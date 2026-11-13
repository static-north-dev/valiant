namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

public sealed class EdgeThrowingChild
{
    public string? InnerValue { get; init; }

    public int Value => InnerValue!.Length;
}
