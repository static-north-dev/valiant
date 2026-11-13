namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

public sealed class EdgeDynamicMessageInput
{
    public string Value { get; init; } = "present";
    public int Calls { get; private set; }
    public string Message()
    {
        Calls++;
        return "Missing value";
    }
}
