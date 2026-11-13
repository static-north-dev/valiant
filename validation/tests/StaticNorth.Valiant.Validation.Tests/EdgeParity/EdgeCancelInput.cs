namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

public sealed record EdgeCancelInput(CancellationTokenSource Cancellation)
{
    public string Value { get; } = "value";
}
