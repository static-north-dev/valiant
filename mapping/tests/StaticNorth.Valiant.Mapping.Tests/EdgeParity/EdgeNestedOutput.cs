namespace StaticNorth.Valiant.Mapping.Tests.EdgeParity;

public sealed record EdgeNestedOutput(EdgeInnerDto? Value)
{
    public EdgeInnerDto[][] Groups { get; init; } = [];
}
