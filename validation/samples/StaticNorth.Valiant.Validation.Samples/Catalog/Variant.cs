namespace Valiant.Validation.Samples.Catalog;

/// <summary>
/// Represents a sample catalog item variant.
/// </summary>
public sealed record Variant
{
    public string? Code { get; init; }

    public int Quantity { get; init; }
}
