namespace Valiant.Validation.Samples.AttributeValidation;

/// <summary>
/// Demonstrates declaring validation rules with attributes and using the
/// generated validator through both synchronous and asynchronous APIs.
/// </summary>
public static class Sample
{
    public static async Task RunAsync()
    {
        var validator = new CreateUserRequestValidator();

        var valid = new CreateUserRequest
        {
            UserName = "ada",
            DisplayName = "Ada Lovelace"
        };

        Console.WriteLine($"Attribute-based valid result: {validator.IsValid(valid)} / {await validator.IsValidAsync(valid)}");

        var invalid = new CreateUserRequest
        {
            UserName = "",
            DisplayName = "This display name is intentionally too long for validation."
        };

        var result = validator.Validate(invalid);
        var asyncResult = await validator.ValidateAsync(invalid);
        Console.WriteLine($"Attribute-based invalid result: {result.IsValid}; async failures: {asyncResult.Errors.Count}");
    }
}
