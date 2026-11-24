namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

public sealed class EdgeSeverityInput
{
    public int Calls { get; private set; }
    public string Value { get; init; } = string.Empty;
    public ValiantSeverity Severity() => ++Calls == 1 ? ValiantSeverity.Error : ValiantSeverity.Warning;
}
