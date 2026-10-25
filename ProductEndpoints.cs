using Microsoft.Data.Sqlite;

namespace PriceMarket;

public static class ProductEndpoints
{
    /* Registers the product list and product search endpoints.
       Program only needs one line: app.MapProductEndpoints(ConnectionString); */
    public static void MapProductEndpoints(this IEndpointRouteBuilder app, string connectionString)
    {
        // Products of a category and all of its subcategories
        app.MapGet("/api/products", async (string? categoryId, string? sort, string? retailer, int? page, int? pageSize) =>
        {
            if (string.IsNullOrEmpty(categoryId))
            {
                return Results.BadRequest(new { error = "CategoryId is required." });
            }

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var results = await GetPageAsync(
                connection,
                @"WITH RECURSIVE SubCats(Id) AS (
                      SELECT Id FROM Categories WHERE Id = @categoryId
                      UNION ALL
                      SELECT c.Id FROM Categories c JOIN SubCats sc ON c.ParentId = sc.Id
                  )",
                "p.CategoryId IN (SELECT Id FROM SubCats)",
                cmd => cmd.Parameters.AddWithValue("@categoryId", categoryId),
                sort, retailer, page, pageSize);

            return Results.Ok(results);
        });

        // Search where every typed word must be the start of some word in the product name
        app.MapGet("/api/products/search", async (string? q, string? sort, string? retailer, int? page, int? pageSize) =>
        {
            var cleanQuery = TextNormalizer.ForSearch(q);
            if (cleanQuery.Length < 2 || cleanQuery.Length > 50)
            {
                return Results.Ok(new List<object>());
            }

            // Only the first 5 words count, so nobody can build a huge query
            var words = cleanQuery.Split(' ').Take(5).ToArray();
            var conditions = words.Select((_, i) => $"(p.NameSearch LIKE @start{i} OR p.NameSearch LIKE @word{i})");

            /* Relevance score, lower is better. Each typed word adds:
               0 when it is a whole word of the name (γαλα in "Γάλα Δέλτα")
               1 when it starts a later word (γαλα in "Σοκολάτα Γάλακτος" or "Αντηλιακό Γαλάκτωμα")
               2 when only the first word starts with it, which is usually the brand (ΓΑΛΑΞΙΑΣ Αλάτι). */
            var score = string.Join(" + ", words.Select((_, i) =>
                $"(CASE WHEN (' ' || p.NameSearch || ' ') LIKE @exact{i} THEN 0 WHEN p.NameSearch LIKE @word{i} THEN 1 ELSE 2 END)"));

            /* Among equally relevant products: the earlier the first typed word appears in the
               name the better, then shorter names first, then alphabetical. */
            var relevanceOrder = $"{score}, instr(' ' || p.NameSearch, @firstWord), length(p.NameSearch), p.NameSearch, p.Id";

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var results = await GetPageAsync(
                connection,
                null,
                string.Join(" AND ", conditions),
                cmd =>
                {
                    for (int i = 0; i < words.Length; i++)
                    {
                        cmd.Parameters.AddWithValue($"@start{i}", words[i] + "%");
                        cmd.Parameters.AddWithValue($"@word{i}", "% " + words[i] + "%");
                        cmd.Parameters.AddWithValue($"@exact{i}", "% " + words[i] + " %");
                    }
                    cmd.Parameters.AddWithValue("@firstWord", " " + words[0]);
                },
                sort, retailer, page, pageSize, relevanceOrder);

            return Results.Ok(results);
        });
    }

    /* Returns one page of products. The caller provides the category or search condition,
       this method adds the optional supermarket filter, the sort order and the paging. */
    private static async Task<List<object>> GetPageAsync(
        SqliteConnection connection,
        string? cteSql,
        string whereSql,
        Action<SqliteCommand> addParameters,
        string? sort, string? retailer, int? page, int? pageSize,
        string? defaultOrderBy = null)
    {
        var size = Math.Clamp(pageSize ?? 36, 1, 100);
        var offset = (Math.Clamp(page ?? 1, 1, 10000) - 1) * size;
        var hasRetailer = !string.IsNullOrWhiteSpace(retailer);

        /* Only fixed text is placed into the SQL below, never text typed by the user.
           The sort value just picks one of three prepared ORDER BY strings. */
        var priceColumn = hasRetailer ? "rp.Price" : "p.MinimumPrice";
        var orderBy = sort switch
        {
            "price-asc" => $"{priceColumn} ASC, p.Id",
            "price-desc" => $"{priceColumn} DESC, p.Id",
            _ => defaultOrderBy ?? "p.NameSearch, p.Id"
        };

        /* With a supermarket selected we join its price row. Only products it sells remain,
           and we can show and sort by its own price. NOCASE ignores upper or lower case. */
        var retailerJoin = hasRetailer
            ? "JOIN ProductPrices rp ON rp.ProductId = p.Id AND rp.RetailerName = @retailer COLLATE NOCASE"
            : "";

        using var command = connection.CreateCommand();
        command.CommandText = $@"
            {cteSql}
            SELECT p.Id, p.Name, p.MinimumPrice, p.ProductBio, p.ImageURL, {(hasRetailer ? "rp.Price" : "NULL")}
            FROM Products p
            {retailerJoin}
            WHERE {whereSql}
            ORDER BY {orderBy}
            LIMIT @limit OFFSET @offset;";

        addParameters(command);
        if (hasRetailer) command.Parameters.AddWithValue("@retailer", retailer!.Trim());
        command.Parameters.AddWithValue("@limit", size);
        command.Parameters.AddWithValue("@offset", offset);

        using var reader = await command.ExecuteReaderAsync();
        var results = new List<object>();

        while (await reader.ReadAsync())
        {
            results.Add(new
            {
                Id = reader.IsDBNull(0) ? null : reader.GetString(0),
                Name = reader.IsDBNull(1) ? null : reader.GetString(1),
                MinimumPrice = reader.IsDBNull(2) ? (decimal?)null : reader.GetDecimal(2),
                ProductBio = reader.IsDBNull(3) ? null : reader.GetString(3),
                ImageURL = reader.IsDBNull(4) ? null : reader.GetString(4),
                RetailerPrice = reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5)
            });
        }

        return results;
    }
}