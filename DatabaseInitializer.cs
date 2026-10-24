using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace PriceMarket;

public static class DatabaseInitializer
{
    private const string ConnectionString = "Data Source=PriceMarket.db";

    public static async Task InitializeAsync()
    {   
        // Activate foreign keys SQLite 
        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        // Read database.sql script
        string sql = await File.ReadAllTextAsync("database.sql");

        using var command = connection.CreateCommand();
        // Activate foreign key support for the current connection
        command.CommandText = "PRAGMA foreign_keys = ON;\n" + sql;
        await command.ExecuteNonQueryAsync();
    }
}