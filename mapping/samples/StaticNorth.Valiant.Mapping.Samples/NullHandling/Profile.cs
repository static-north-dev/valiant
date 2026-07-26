namespace StaticNorth.Valiant.Mapping.Samples.NullHandling;

public sealed record Profile
{
    public string? DefaultValue { get; init; }

    public string? IgnoredValue { get; init; }

    public string? MappedValue { get; init; }

    public string? AllowedValue { get; init; }

    public string? FallbackValue { get; init; }
}
