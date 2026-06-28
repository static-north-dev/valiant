namespace Valiant.Validation.Sample.Models;

public sealed record Address
{
    public string? Line1 { get; init; }

    public string? Line2 { get; init; }

    public string? Zip { get; init; }
}
