using System.Text.Json.Serialization;

namespace Valiant.Validation.AspNetCore.Samples.Orders;

/// <summary>
/// Represents a sample order creation request.
/// </summary>
public sealed record CreateOrder
{
    [JsonPropertyName("customerEmail")]
    public string? CustomerEmail { get; init; }

    [JsonPropertyName("lines")]
    public List<CreateOrderLine> Lines { get; init; } = [];

    [JsonPropertyName("total")]
    public decimal Total { get; init; }

    [JsonPropertyName("isVip")]
    public bool IsVip { get; init; }

    [JsonPropertyName("vipCode")]
    public string? VipCode { get; init; }
}
