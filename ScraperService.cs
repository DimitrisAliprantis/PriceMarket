using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace PriceMarket;

public static class ScraperService
{
    public static async Task RunAsync(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        using var client = new HttpClient();

        // Set standard browser headers to avoid request blocking by the API
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        client.DefaultRequestHeaders.Add("Origin", "https://posokanei.gov.gr");
        client.DefaultRequestHeaders.Add("Referer", "https://posokanei.gov.gr/");

        Console.WriteLine("Fetching category tree...");
        string treeUrl = "https://api.posokanei.gov.gr/meta/categories/tree?include_counts=true&include_hidden=false";

        var treeResponse = await client.GetFromJsonAsync<CategoryTreeResponse>(treeUrl);

        if (treeResponse == null || treeResponse.Tree.Count == 0)
        {
            Console.WriteLine("Failed to load category tree.");
            return;
        }

        // Save category hierarchy and return only leaf categories that contain products
        var targetCategoryIds = await CategoryService.ProcessCategoryTreeAsync(treeResponse.Tree, connection);

        Console.WriteLine($"Stored categories in DB. Found {targetCategoryIds.Count} leaf categories with products.\n");

        int totalProductsScraped = 0;
        int totalSkipped = 0;

        foreach (var categoryId in targetCategoryIds)
        {
            int currentPage = 1;
            int totalPages = 1;

            do
            {
                // Build paginated endpoint URL for the current category sorted by unit price
                string productsUrl = $"https://api.posokanei.gov.gr/products?page={currentPage}&page_size=36&sort_by=unit_price&sort_order=asc&category={categoryId}&countries=GR";

                /*
                 * Wrap each page request in a try-catch block to isolate network or parsing failures.
                 * If a single page fails, we log the issue and move on to the next category
                 * without crashing the entire scraping process.
                 */
                try
                {
                    // Deserialize only the required product fields defined in our C# model
                    var data = await client.GetFromJsonAsync<PriceMarketResponse>(productsUrl);

                    if (data?.Products != null && data.Products.Count > 0)
                    {
                        // Update loop boundary with the actual total pages reported by the API
                        totalPages = data.TotalPages;

                        Console.WriteLine($"Category [{categoryId}] - Page {data.Page}/{data.TotalPages} - Fetched {data.Products.Count} items.");

                        totalProductsScraped += data.Products.Count;

                        totalSkipped += await SaveProductsAsync(data.Products, categoryId, connection);
                    }
                    else
                    {
                        // Stop pagination early if the API returns an empty product list
                        break;
                    }
                }
                catch (Exception ex)
                {
                    // Log the error and skip the remaining pages of the current category
                    Console.WriteLine($"Error fetching category {categoryId}, page {currentPage}: {ex.Message}");
                    break;
                }

                currentPage++;

                // Small delay between requests to avoid triggering rate limits
                await Task.Delay(100);

            } while (currentPage <= totalPages);
        }

        Console.WriteLine($"\nScraping finished. Total items processed: {totalProductsScraped}, skipped: {totalSkipped}");
    }

    /* Saves a batch of products and their corresponding supermarket prices.
      Uses a single SQLite transaction and pre-allocated SQL parameters to minimize
      disk I/O overhead and maximize bulk insert speed. */
    private static async Task<int> SaveProductsAsync(List<ProductDetails> products, string categoryId, SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        /*  Safety fallback: Ensure any retailer name referenced by a product exists in the Supermarkets table.
           INSERT OR IGNORE preserves existing rows (keeping their hardcoded LogoURL intact)
           while preventing foreign key constraint failures for unseeded retailers. */
        using var ensureMarketCmd = connection.CreateCommand();
        ensureMarketCmd.Transaction = transaction;
        ensureMarketCmd.CommandText = """
            INSERT OR IGNORE INTO Supermarkets (Name, LogoURL)
            VALUES (@MarketName, NULL);
        """;
        var pMarketName = ensureMarketCmd.Parameters.Add("@MarketName", SqliteType.Text);

        // Prepare parameterized command for inserting or updating core product records
        using var productCmd = connection.CreateCommand();
        productCmd.Transaction = transaction;
        productCmd.CommandText = """
        INSERT OR REPLACE INTO Products (Id, Name, NameSearch, MinimumPrice, ImageURL, CategoryId, ProductBio)
        VALUES (@Id, @Name, @NameSearch, @MinimumPrice, @ImageURL, @CategoryId, @ProductBio);
        """;

        var pNameSearch = productCmd.Parameters.Add("@NameSearch", SqliteType.Text);
        var pId = productCmd.Parameters.Add("@Id", SqliteType.Text);
        var pName = productCmd.Parameters.Add("@Name", SqliteType.Text);
        var pMinimumPrice = productCmd.Parameters.Add("@MinimumPrice", SqliteType.Real);
        var pCategoryId = productCmd.Parameters.Add("@CategoryId", SqliteType.Text);
        var pImageURL = productCmd.Parameters.Add("@ImageURL", SqliteType.Text);
        var pProductBio = productCmd.Parameters.Add("@ProductBio", SqliteType.Text);

        pCategoryId.Value = categoryId;

        // Prepare parameterized command for inserting product prices per supermarket
        using var priceCmd = connection.CreateCommand();
        priceCmd.Transaction = transaction;
        priceCmd.CommandText = """
            INSERT OR REPLACE INTO ProductPrices (ProductId, RetailerName, Price)
            VALUES (@ProductId, @RetailerName, @Price);
        """;

        var pPriceProductId = priceCmd.Parameters.Add("@ProductId", SqliteType.Text);
        var pRetailerName = priceCmd.Parameters.Add("@RetailerName", SqliteType.Text);
        var pPrice = priceCmd.Parameters.Add("@Price", SqliteType.Real);

        // Counts products we refuse to store, so bad data is visible instead of silent
        int skipped = 0;

        foreach (var product in products)
        {
            /* A product without an id or a name can't be stored (the id is the primary key
               and the name is NOT NULL), so skip it before touching the database. */
            if (string.IsNullOrWhiteSpace(product.Id) || string.IsNullOrWhiteSpace(product.Name))
            {
                skipped++;
                Console.WriteLine("Skipped a product with a missing id or name.");
                continue;
            }

            /* Collect only the retailer prices we can trust. A price is unusable if it is
               missing or not positive, because a price of 0 would wrongly look like the
               cheapest option. A blank retailer name can't be linked to a supermarket either. */
            var validPrices = new List<(string Retailer, decimal Price)>();

            if (product.ProductPricePerRetailer != null)
            {
                foreach (var rp in product.ProductPricePerRetailer)
                {
                    var retailerName = rp.RetailerDisplayName?.Trim();

                    if (string.IsNullOrEmpty(retailerName) || rp.Price is null or <= 0)
                        continue;

                    validPrices.Add((retailerName, rp.Price.Value));
                }
            }

            /* A product with no usable price is useless to a price comparison site.
               Skipping it here also avoids the NOT NULL failure on MinimumPrice,
               which used to fail the whole page. */
            if (validPrices.Count == 0)
            {
                skipped++;
                Console.WriteLine($"Skipped product {product.Id}: no valid prices.");
                continue;
            }

            /* MinimumPrice is computed from the valid prices, so it always matches
            what ends up in ProductPrices instead of trusting the API's price_stats. */
            pId.Value = product.Id;
            pName.Value = product.Name;
            // Store the cleaned copy of the name that the search endpoint will match against
            pNameSearch.Value = TextNormalizer.ForSearch(product.Name);
            pMinimumPrice.Value = validPrices.Min(p => p.Price);
            pImageURL.Value = product.ImageURL ?? (object)DBNull.Value;
            pProductBio.Value = product.ProductBio ?? (object)DBNull.Value;

            await productCmd.ExecuteNonQueryAsync();

            foreach (var (retailer, price) in validPrices)
            {
                // Register the retailer if it is new, so the foreign key doesn't fail
                pMarketName.Value = retailer;
                await ensureMarketCmd.ExecuteNonQueryAsync();

                pPriceProductId.Value = product.Id;
                pRetailerName.Value = retailer;
                pPrice.Value = price;

                await priceCmd.ExecuteNonQueryAsync();
            }
        }

        // Commit all product and price inserts in one batch
        await transaction.CommitAsync();

        return skipped;
    }
}