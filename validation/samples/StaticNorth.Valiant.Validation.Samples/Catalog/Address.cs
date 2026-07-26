namespace StaticNorth.Valiant.Validation.Samples.Catalog;

/// <summary>
/// Represents a sample postal address.
/// </summary>
public sealed record Address
{
    public string? Line1 { get; init; }

    public string? Line2 { get; init; }

    public string? Zip { get; init; }
}
