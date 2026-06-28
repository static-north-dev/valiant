using System.Text.Json.Serialization;

namespace Valiant.AspNetCore.Sample.Models;

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
