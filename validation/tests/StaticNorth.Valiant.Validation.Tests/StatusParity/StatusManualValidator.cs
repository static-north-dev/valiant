namespace StaticNorth.Valiant.Validation.Tests.StatusParity;

public sealed class StatusManualValidator : IValidator<StatusParityModel>
{
    public ValidationResult Validate(StatusParityModel instance)
    {
        instance.ReuseMethod = nameof(Validate);
        return ValidationResult.Valid;
    }

    public ValueTask<ValidationResult> ValidateAsync(StatusParityModel instance, CancellationToken cancellationToken = default)
    {
        instance.ReuseMethod = nameof(ValidateAsync);
        return new ValueTask<ValidationResult>(ValidationResult.Valid);
    }

    public bool IsValid(StatusParityModel instance) => throw new InvalidOperationException("Expected Validate to be called.");

    public ValueTask<bool> IsValidAsync(StatusParityModel instance, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Expected ValidateAsync to be called.");
}
