namespace StaticNorth.Valiant.Validation;

/// <summary>
/// Validates property values reused from a parent validator.
/// </summary>
/// <typeparam name="T">The property value type being validated.</typeparam>
public interface IPropertyValidator<in T>
{
    /// <summary>
    /// Validates the supplied property value.
    /// </summary>
    /// <param name="value">The property value to validate.</param>
    /// <returns>The validation result.</returns>
    ValidationResult Validate(T value);

    /// <summary>
    /// Validates the supplied property value, including asynchronous validations.
    /// </summary>
    /// <param name="value">The property value to validate.</param>
    /// <param name="cancellationToken">A cancellation token for asynchronous validations.</param>
    /// <returns>The validation result.</returns>
    ValueTask<ValidationResult> ValidateAsync(T value, CancellationToken cancellationToken = default);
}
