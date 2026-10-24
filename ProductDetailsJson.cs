using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Net.Http.Headers;

namespace PriceMarket;

public class ProductDetails
{   
    [JsonPropertyName("price_stats")] 
    public ProductMinimumPrice? ProductMinimumPrice { get; set; }

    [JsonPropertyName("retailer_prices")]
    public List<ProductPricePerRetailer>? ProductPricePerRetailer { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageURL { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("description")]
    public string? ProductBio { get; set; }

    
}


public class ProductMinimumPrice
{
    // we allow price to be null so when scraping it doesnt throw an exception if the price is missing, we can just ignore it and move on to the next product
    [JsonPropertyName("min_price")]
    public decimal? MinimumPrice { get; set; }

}


public class ProductPricePerRetailer
{
    
    [JsonPropertyName("retailer_display_name")]
    public string? RetailerDisplayName { get; set; }

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

}

public class RetailerInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("logo_url")]
    public string? LogoURL { get; set;}

    [JsonPropertyName("website")]
    public string? WebsiteURL { get; set;}
}

public class Supermarkets
{
    [JsonPropertyName("retailers")]
    public List<RetailerInfo> ? RetailerInfo { get; set;}
    
}