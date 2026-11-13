namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

public sealed class EdgeChildInput
{
    public EdgeChildValue? Child { get; init; } = new();

    public List<EdgeChildValue> Items { get; init; } = [];
}
