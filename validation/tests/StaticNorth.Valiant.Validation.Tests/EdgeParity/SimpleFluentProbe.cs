namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

public sealed class SimpleFluentProbe
{
    public string? NameValue { get; init; }

    public int? AgeValue { get; init; }

    public bool ThrowOnAge { get; init; }

    public CancellationTokenSource? Cancellation { get; init; }

    public List<string> Reads { get; } = [];

    public string? Name
    {
        get
        {
            Reads.Add(nameof(Name));
            Cancellation?.Cancel();
            return NameValue;
        }
    }

    public int? Age
    {
        get
        {
            Reads.Add(nameof(Age));
            return ThrowOnAge ? throw new InvalidOperationException("Age getter failed.") : AgeValue;
        }
    }
}
