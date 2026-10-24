using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PriceMarket;

public class PriceMarketResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("has_next")]
    public bool HasNext { get; set; }

    [JsonPropertyName("products")]
    public List<ProductDetails>? Products { get; set; }
}