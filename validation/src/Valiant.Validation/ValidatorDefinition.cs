namespace Valiant.Validation;

/// <summary>
/// Base type for source-generated validator definitions that can use attribute-based default validation.
/// </summary>
/// <typeparam name="T">The model type being validated.</typeparam>
public abstract class ValidatorDefinition<T> : IValidatorDefinition<T>
{
    public virtual void Configure(ValidationContext<T> context) { }
}
