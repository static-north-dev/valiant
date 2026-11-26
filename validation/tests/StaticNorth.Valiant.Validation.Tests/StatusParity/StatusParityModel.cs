namespace StaticNorth.Valiant.Validation.Tests.StatusParity;

public sealed class StatusParityModel
{
    public string? Missing { get; init; }

    public string Present { get; init; } = "present";

    public StatusParityModel? Child { get; init; }

    public StatusParityRank Rank { get; init; } = new(2);

    public StatusParityRank MinimumRank { get; init; } = new(1);

    public bool Enabled { get; private set; }

    public int DiagnosticCalls { get; private set; }

    public int TailCalls { get; private set; }

    public string? ReuseMethod { get; set; }

    public bool RunTail(bool result)
    {
        TailCalls++;
        return result;
    }

    public string EnableRule()
    {
        DiagnosticCalls++;
        Enabled = true;
        return "Enabled";
    }

    public bool ThrowTail()
    {
        TailCalls++;
        throw new InvalidOperationException("tail predicate failed");
    }

    public string ThrowMessage()
    {
        DiagnosticCalls++;
        throw new InvalidOperationException("message callback failed");
    }
}
