namespace StaticNorth.Valiant.Mapping.Tests;

public sealed class SingleReadSource(bool firstReadIsNull, bool skipValue)
{
    private int _constructorReads;
    private int _initReads;
    private int _valueReads;

    public int ConstructorReads => _constructorReads;
    public int InitReads => _initReads;
    public int ValueReads => _valueReads;

    public bool SkipValue { get; } = skipValue;

    public string? ConstructorValue => Read(ref _constructorReads);
    public string? InitValue => Read(ref _initReads);
    public string? Value => Read(ref _valueReads);

    private string? Read(ref int reads)
    {
        reads++;
        return reads == 1 ? (firstReadIsNull ? null : "first") : "second";
    }
}
