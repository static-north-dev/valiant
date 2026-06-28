namespace Valiant.Validation.Sample.Models;

public sealed record Variant
{
    public string? Code { get; init; }

    public int Quantity { get; init; }
}
