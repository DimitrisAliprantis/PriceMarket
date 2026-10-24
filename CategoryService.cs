using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace PriceMarket;

public static class CategoryService
{
    public static async Task<List<string>> ProcessCategoryTreeAsync(List<CategoryNode> rootNodes, SqliteConnection connection)
    {
        var leafCategoryIds = new List<string>();

        using var transaction = connection.BeginTransaction();
        var insertCmd = connection.CreateCommand();
        insertCmd.Transaction = transaction;
        insertCmd.CommandText = """
            INSERT OR REPLACE INTO Categories (Id, Name, ImageURL, ParentId)
            VALUES (@Id, @Name, @ImageURL, @ParentId);
        """;

        var pId = insertCmd.Parameters.Add("@Id", SqliteType.Text);
        var pName = insertCmd.Parameters.Add("@Name", SqliteType.Text);
        var pImageURL = insertCmd.Parameters.Add("@ImageURL", SqliteType.Text);
        var pParent = insertCmd.Parameters.Add("@ParentId", SqliteType.Text);

        async Task TraverseAsync(CategoryNode node, string? parentId)
        {
            pId.Value = node.CategoryId;
            pName.Value = node.Name;
            pParent.Value = (object?)parentId ?? DBNull.Value;
            pImageURL.Value = (object?)node.ImageURL ?? DBNull.Value;
            await insertCmd.ExecuteNonQueryAsync();

            if (node.Children == null || node.Children.Count == 0)
            {
                if (node.TotalProductCount > 0)
                {
                    leafCategoryIds.Add(node.CategoryId);
                }
            }
            else
            {
                foreach (var child in node.Children)
                {
                    await TraverseAsync(child, node.CategoryId);
                }
            }
        }

        foreach (var root in rootNodes)
        {
            await TraverseAsync(root, null);
        }

        await transaction.CommitAsync();
        return leafCategoryIds;
    }
}