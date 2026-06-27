namespace Valiant;

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
    /// Validates the supplied instance, including asynchronous rules.
    /// </summary>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="cancellationToken">A cancellation token for asynchronous validation rules.</param>
    /// <returns>The validation result.</returns>
    global::System.Threading.Tasks.ValueTask<ValidationResult> ValidateAsync(T instance, global::System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the supplied instance is valid.
    /// </summary>
    /// <param name="instance">The instance to validate.</param>
    /// <returns><c>true</c> when the instance is valid; otherwise, <c>false</c>.</returns>
    bool IsValid(T instance);

    /// <summary>
    /// Determines whether the supplied instance is valid, including asynchronous rules.
    /// </summary>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="cancellationToken">A cancellation token for asynchronous validation rules.</param>
    /// <returns><c>true</c> when the instance is valid; otherwise, <c>false</c>.</returns>
    global::System.Threading.Tasks.ValueTask<bool> IsValidAsync(T instance, global::System.Threading.CancellationToken cancellationToken = default);
}
