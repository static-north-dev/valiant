namespace StaticNorth.Valiant.Validation.Samples.Catalog;

/// <summary>
/// Demonstrates reusable property validation for sample addresses.
/// </summary>
[ValiantValidator]
public sealed class AddressPropertyValidator(AddressValidator validator) : PropertyValidatorDefinition<Address>
{
    public override ValidationResult Validate(Address value)
    {
        return validator.Validate(value);
    }

    public override ValueTask<ValidationResult> ValidateAsync(Address value, CancellationToken cancellationToken = default)
    {
        return validator.ValidateAsync(value, cancellationToken);
    }
}
