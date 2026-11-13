namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

public sealed class EdgeChildValue
{
    public int Calls { get; private set; }
    public string Value { get; init; } = string.Empty;
    public string Counted
    {
        get
        {
            Calls++;
            return Value;
        }
    }
}
