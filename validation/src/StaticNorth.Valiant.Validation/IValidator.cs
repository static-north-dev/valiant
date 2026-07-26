namespace StaticNorth.Valiant.Validation;

/// <summary>
/// Validates instances of <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The model type being validated.</typeparam>
public interface IValidator<in T>
{
    /// <summary>
    /// Validates the supplied instance.
    /// </summary>
    /// <param name="instance">The instance to validate.</param>
    /// <returns>The validation result.</returns>
    ValidationResult Validate(T instance);

    /// <summary>
    /// Validates the supplied instance, including asynchronous validations.
    /// </summary>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="cancellationToken">A cancellation token for asynchronous validations.</param>
    /// <returns>The validation result.</returns>
    ValueTask<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the supplied instance is valid.
    /// </summary>
    /// <param name="instance">The instance to validate.</param>
    /// <returns><c>true</c> when the instance is valid; otherwise, <c>false</c>.</returns>
    bool IsValid(T instance);

    /// <summary>
    /// Determines whether the supplied instance is valid, including asynchronous validations.
    /// </summary>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="cancellationToken">A cancellation token for asynchronous validations.</param>
    /// <returns><c>true</c> when the instance is valid; otherwise, <c>false</c>.</returns>
    ValueTask<bool> IsValidAsync(T instance, CancellationToken cancellationToken = default);
}
