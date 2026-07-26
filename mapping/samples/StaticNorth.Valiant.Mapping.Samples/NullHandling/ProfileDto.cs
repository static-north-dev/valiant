namespace StaticNorth.Valiant.Mapping.Samples.NullHandling;

public sealed record ProfileDto
{
    public string? DefaultValue { get; set; } = "existing default";

    public string? IgnoredValue { get; set; } = "existing ignored";

    public string? MappedValue { get; set; } = "existing mapped";

    public string? AllowedValue { get; set; } = "existing allowed";

    public string? FallbackValue { get; set; } = "existing fallback";
}
