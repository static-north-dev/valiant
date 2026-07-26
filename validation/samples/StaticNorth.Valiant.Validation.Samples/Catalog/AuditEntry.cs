namespace StaticNorth.Valiant.Validation.Samples.Catalog;

/// <summary>
/// Represents a sample audit entry.
/// </summary>
public sealed record AuditEntry
{
    public string? Actor { get; init; }
}
