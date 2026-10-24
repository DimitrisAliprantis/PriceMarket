using System.Text.Json.Serialization;

namespace PriceMarket;

public class CategoryTreeResponse
{
    [JsonPropertyName("total_categories")]
    public int TotalCategories { get; set; }

    [JsonPropertyName("root_count")]
    public int RootCount { get; set; }

    [JsonPropertyName("tree")]
    public List<CategoryNode> Tree { get; set; } = new();
}

public class CategoryNode
{
    [JsonPropertyName("category_id")]
    public string CategoryId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("image_url")]
    public string ImageURL { get; set;} = string.Empty;

    [JsonPropertyName("depth")]
    public int Depth { get; set; }

    [JsonPropertyName("total_product_count")]
    public int TotalProductCount { get; set; }

    [JsonPropertyName("children")]
    public List<CategoryNode>? Children { get; set; }
}