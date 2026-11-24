namespace StaticNorth.Valiant.Validation.Tests;

public sealed class ParityChangingProperty
{
    public int Reads { get; private set; }

    public string? Value => ++Reads == 1 ? null : "present";

    public bool EnableAsyncRule { get; init; }
}
