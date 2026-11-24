namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

public sealed class EdgeReuseInput
{
    public string? Value { get; init; }
    public int TailCalls { get; private set; }
    public bool Tail()
    {
        TailCalls++;
        return false;
    }
}
