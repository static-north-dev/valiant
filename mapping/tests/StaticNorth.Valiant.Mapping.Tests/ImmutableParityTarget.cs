namespace StaticNorth.Valiant.Mapping.Tests;

public sealed class ImmutableParityTarget
{
    public ImmutableParityTarget(string? name, int unused)
    {
        Name = name;
        Constructor = $"unmapped({unused})";
    }

    public ImmutableParityTarget(object? name)
    {
        if (name is "invalid")
        {
            throw new InvalidOperationException("Cannot construct target.");
        }

        Name = (string?)name;
        Constructor = "constructor";
    }

    public ImmutableParityTarget(string? name)
    {
        Name = name;
        Constructor = "other overload";
    }

    public string? Name { get; }

    public long Number { get; init; }

    public long[]? Numbers { get; init; }

    public string Constructor { get; }
}
