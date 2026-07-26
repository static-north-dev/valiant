namespace StaticNorth.Valiant.Validation;

/// <summary>
/// Defines the validation declaration contract for a source-generated validator.
/// </summary>
/// <typeparam name="T">The model type being validated.</typeparam>
public interface IValidatorDefinition<T>
{
    /// <summary>
    /// Configures validation rules for <typeparamref name="T"/>.
    /// </summary>
    /// <param name="context">The validation declaration context.</param>
    void Configure(ValidationContext<T> context);
}
