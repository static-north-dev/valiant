namespace Valiant.Sample.Models;

public sealed record AuditEntry
{
    public string? Actor { get; init; }
}
