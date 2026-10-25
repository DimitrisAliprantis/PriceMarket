using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PriceMarket;
using Microsoft.AspNetCore.Builder;

namespace PriceMarket;

class Program
{
    private const string ConnectionString = "Data Source=PriceMarket.db";

    static async Task Main(string[] args)
    {
        // Database initialization
        Console.WriteLine("Initializing database...");
        await DatabaseInitializer.InitializeAsync();
        Console.WriteLine("Database ready: PriceMarket.db created/verified.\n");

        Console.WriteLine("Would you like to update the data?");
        Console.WriteLine("Yes(1)");
        Console.WriteLine("No(0)");

        int userChoice = Convert.ToInt32(Console.ReadLine());
        if(userChoice == 1){
            await ScraperService.RunAsync(ConnectionString);
        }

        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseHttpsRedirection();

        /* Catches any exception that escapes an endpoint. The middleware logs the full
        error to the console automatically, then we send the client a generic message. */
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                await Results.Problem(
                    title: "Something went wrong on our side.",
                    statusCode: 500
                ).ExecuteAsync(context);
            });
        });

        // Return categories with SubCategoryCount (how many children does each have)
        app.MapGet("/api/categories", async (string? parentId) =>
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();

                using var command = connection.CreateCommand();

                if (string.IsNullOrEmpty(parentId))
                {
                    command.CommandText = @"
                        SELECT c.Id, c.Name, c.ImageURL,
                            (SELECT COUNT(*) FROM Categories sub WHERE sub.ParentId = c.Id) AS SubCategoryCount
                        FROM Categories c
                        WHERE c.ParentId IS NULL OR c.ParentId = '';";
                }
                else
                {
                    command.CommandText = @"
                        SELECT c.Id, c.Name, c.ImageURL,
                            (SELECT COUNT(*) FROM Categories sub WHERE sub.ParentId = c.Id) AS SubCategoryCount
                        FROM Categories c
                        WHERE c.ParentId = @parentId;";
                    command.Parameters.AddWithValue("@parentId", parentId);
                }

                using var reader = await command.ExecuteReaderAsync();
                var results = new List<object>();

                while (await reader.ReadAsync())
                {
                    results.Add(new
                    {
                        Id = reader.GetString(0),
                        Name = reader.GetString(1),
                        ImageURL = reader.IsDBNull(2) ? null : reader.GetString(2),
                        SubCategoryCount = reader.GetInt32(3)
                    });
                }

                return Results.Ok(results);
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });

        

        // Endpoint to fetch all supermarkets with their logos
        app.MapGet("/api/supermarkets", async () =>
        {
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Name, LogoURL FROM Supermarkets ORDER BY Name ASC;";

            using var reader = await command.ExecuteReaderAsync();
            var supermarkets = new List<object>();

            while (await reader.ReadAsync())
            {
                supermarkets.Add(new
                {
                    name = reader.GetString(0),
                    logoUrl = reader.IsDBNull(1) ? null : reader.GetString(1)
                });
            }

            return Results.Ok(supermarkets);
        });

        // Endpoint to fetch prices and supermarket logo for a specific product
        app.MapGet("/api/productprices", async (string productId) =>
        {
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            // Join with Supermarkets table to pull the LogoURL alongside each price
            command.CommandText = """
                SELECT pp.RetailerName, pp.Price, s.LogoURL
                FROM ProductPrices pp
                LEFT JOIN Supermarkets s ON pp.RetailerName = s.Name
                WHERE pp.ProductId = @productId
                ORDER BY pp.Price ASC;
            """;
            command.Parameters.AddWithValue("@productId", productId);

            using var reader = await command.ExecuteReaderAsync();
            var prices = new List<object>();

            while (await reader.ReadAsync())
            {
                prices.Add(new
                {
                    retailerName = reader.GetString(0),
                    price = reader.GetDecimal(1),
                    logoUrl = reader.IsDBNull(2) ? null : reader.GetString(2)
                });
            }

            return Results.Ok(prices);
        });

            app.MapProductEndpoints(ConnectionString);
            app.Run();

        
    }
}
