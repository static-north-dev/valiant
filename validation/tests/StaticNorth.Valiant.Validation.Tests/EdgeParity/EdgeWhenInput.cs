namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

public sealed class EdgeWhenInput
{
    public int WhenCalls { get; private set; }
    public string Value { get; init; } = string.Empty;
    public bool TestCondition() => ++WhenCalls == 1;
}
