namespace Valiant.Validation.Sample.Scenarios;

internal static class SampleAssertions
{
    public static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void RequireFailure(ValidationResult result, string propertyName, string code)
    {
        Require(
            result.Failures.Any(failure =>
                failure.PropertyName == propertyName &&
                failure.ErrorCode == code),
            "Expected validation failure '" + code + "' for '" + propertyName + "'.");
    }

    public static void RequireNoFailure(ValidationResult result, string propertyName, string code)
    {
        Require(
            !result.Failures.Any(failure =>
                failure.PropertyName == propertyName &&
                failure.ErrorCode == code),
            "Unexpected validation failure '" + code + "' for '" + propertyName + "'.");
    }
}
