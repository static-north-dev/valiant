namespace StaticNorth.Valiant.Mapping.Tests;

public sealed record ImmutableParitySource(bool Preferred, bool Alternate)
{
    public string? NameOverride { get; init; }

    public string FallbackName { get; init; } = "unknown";

    public int Number { get; init; } = 7;

    public int[]? Numbers { get; init; } = [2, 4];
}
