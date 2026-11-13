namespace StaticNorth.Valiant.Validation.Tests;

public sealed class FixedFailureModel
{
    public int FailedChecks { get; set; }

    public int ReadCount { get; private set; }

    [GreaterThan(0)]
    public int A => Read(1);

    [GreaterThan(0)]
    public int B => Read(2);

    [GreaterThan(0)]
    public int C => Read(4);

    [GreaterThan(0)]
    public int D => Read(8);

    [GreaterThan(0)]
    public int E => Read(16);

    private int Read(int bit)
    {
        ReadCount++;
        return (FailedChecks & bit) == 0 ? 1 : 0;
    }
}
