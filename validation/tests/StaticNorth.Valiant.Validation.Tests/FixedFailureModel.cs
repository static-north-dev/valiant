namespace StaticNorth.Valiant.Validation.Tests;

public sealed class FixedFailureModel
{
    public int FailedChecks { get; set; }

    public int ReadCount { get; private set; }

    public int A => Read(1);

    public int B => Read(2);

    public int C => Read(4);

    public int D => Read(8);

    public int E => Read(16);

    private int Read(int bit)
    {
        ReadCount++;
        return (FailedChecks & bit) == 0 ? 1 : 0;
    }
}
