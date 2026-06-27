namespace Valiant;

/// <summary>
/// Contains the result of a validation operation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets the shared valid result used by generated validators when no failures are present.
    /// </summary>
    public static ValidationResult Valid { get; } = new([]);

    /// <summary>
    /// Initializes a validation result from a failure list.
    /// </summary>
    public ValidationResult(IReadOnlyList<ValidationFailure> errors)
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    /// <summary>
    /// Gets a value indicating whether validation succeeded.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the validation failures.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Errors { get; }
}
