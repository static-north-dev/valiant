using System.Text.Json.Serialization;

namespace Valiant.Validation.AspNetCore.Samples.Orders;

/// <summary>
/// Represents a sample order line.
/// </summary>
public sealed record CreateOrderLine
{
    [JsonPropertyName("sku")]
    public string? Sku { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }
}
