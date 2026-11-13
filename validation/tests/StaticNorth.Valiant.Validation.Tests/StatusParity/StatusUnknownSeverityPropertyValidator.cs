namespace StaticNorth.Valiant.Validation.Tests.StatusParity;

public sealed class StatusUnknownSeverityPropertyValidator : IPropertyValidator<string>
{
    public ValidationResult Validate(string value)
    {
        return new ValidationResult([new ValidationFailure(string.Empty, "Unknown", value, (ValiantSeverity)99)]);
    }

    public ValueTask<ValidationResult> ValidateAsync(string value, CancellationToken cancellationToken = default)
    {
        return new ValueTask<ValidationResult>(Validate(value));
    }
}
