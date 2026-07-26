namespace StaticNorth.Valiant.Validation.Samples.Basic;

/// <summary>
/// Demonstrates the smallest useful Valiant validator: define fluent rules,
/// validate valid and invalid values, and inspect validation failures.
/// </summary>
public static class Sample
{
    public static Task RunAsync()
    {
        var validator = new CreateUserValidator();
        var valid = validator.Validate(new CreateUser("Ada"));
        var invalid = validator.Validate(new CreateUser(""));

        Console.WriteLine($"Valid user accepted: {valid.IsValid}");
        Console.WriteLine($"Invalid user accepted: {invalid.IsValid}");
        foreach (var failure in invalid.Failures)
        {
            Console.WriteLine($"{failure.PropertyName}: {failure.ErrorCode} - {failure.ErrorMessage}");
        }

        return Task.CompletedTask;
    }
}
