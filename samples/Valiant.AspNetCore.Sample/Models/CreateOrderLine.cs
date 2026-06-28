using System.Text.Json.Serialization;

namespace Valiant.AspNetCore.Sample.Models;

public sealed record CreateOrderLine
{
    [JsonPropertyName("sku")]
    public string? Sku { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }
}
