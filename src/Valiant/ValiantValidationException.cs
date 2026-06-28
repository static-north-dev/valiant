namespace Valiant;

/// <summary>
/// Thrown when a validation result contains blocking validation errors.
/// </summary>
public sealed class ValiantValidationException : Exception
{
    /// <summary>
    /// Initializes a validation exception.
    /// </summary>
    /// <param name="result">The failed validation result.</param>
    public ValiantValidationException(ValidationResult result)
        : base("Validation failed.")
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    /// <summary>
    /// Gets the failed validation result.
    /// </summary>
    public ValidationResult Result { get; }

    /// <summary>
    /// Gets the blocking validation errors.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Errors => Result.Errors;
}
