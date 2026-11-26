namespace StaticNorth.Valiant.Mapping.Tests.EdgeParity;

public sealed record EdgeNestedInput(bool UseFirst, EdgeInner Value)
{
    public EdgeInner[][] Groups { get; init; } = [];
}
