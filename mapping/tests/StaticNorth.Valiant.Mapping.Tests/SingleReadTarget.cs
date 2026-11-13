namespace StaticNorth.Valiant.Mapping.Tests;

public sealed class SingleReadTarget(string? constructorValue)
{
    public string? ConstructorValue { get; } = constructorValue;
    public string? InitValue { get; init; }
    public string? Value { get; set; } = "original";
}
