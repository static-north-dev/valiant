namespace Valiant;

/// <summary>
/// Defines the compile-time rule declaration contract for a source-generated validator.
/// </summary>
/// <typeparam name="T">The model type being validated.</typeparam>
public interface IValidatorDefinition<T>
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// Declares validation rules for <typeparamref name="T"/>.
    /// </summary>
    static abstract ValidationContext<T> Configure(ValidationContext<T> context);
#endif
}
