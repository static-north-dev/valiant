namespace StaticNorth.Valiant.Mapping.Tests;

public sealed class AssignmentParitySource(bool skipInit = false, string? value = "mapped")
{
    public bool SkipInit { get; } = skipInit;

    public string? Value { get; } = value;

    public List<string> Calls { get; } = [];

    public string? Read(string name)
    {
        Calls.Add(name);
        return Value;
    }

    public string Fallback()
    {
        Calls.Add("fallback");
        return "fallback";
    }

    public bool UsePrimaryFallback()
    {
        Calls.Add("branch");
        return Calls.Count(call => call == "branch") == 1;
    }

    public string RecordFallback(string value)
    {
        Calls.Add(value);
        return value;
    }
}
