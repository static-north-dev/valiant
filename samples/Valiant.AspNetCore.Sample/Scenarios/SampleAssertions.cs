namespace Valiant.AspNetCore.Sample.Scenarios;

internal static class SampleAssertions
{
    public static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
