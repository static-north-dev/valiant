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
        Failures = errors ?? throw new ArgumentNullException(nameof(errors));
        Errors = FilterBySeverity(Failures, ValiantSeverity.Error);
        Warnings = FilterBySeverity(Failures, ValiantSeverity.Warning);
        Infos = FilterBySeverity(Failures, ValiantSeverity.Info);
    }

    /// <summary>
    /// Gets a value indicating whether validation succeeded.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets every validation failure, including warnings and informational failures.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Failures { get; }

    /// <summary>
    /// Gets validation failures with <see cref="ValiantSeverity.Error" /> severity.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Errors { get; }

    /// <summary>
    /// Gets validation failures with <see cref="ValiantSeverity.Warning" /> severity.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Warnings { get; }

    /// <summary>
    /// Gets validation failures with <see cref="ValiantSeverity.Info" /> severity.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Infos { get; }

    /// <summary>
    /// Throws a <see cref="ValiantValidationException" /> when this result contains validation errors.
    /// </summary>
    /// <returns>The current validation result.</returns>
    public ValidationResult ThrowIfFailed()
    {
        if (!IsValid)
        {
            throw new ValiantValidationException(this);
        }

        return this;
    }

    private static IReadOnlyList<ValidationFailure> FilterBySeverity(
        IReadOnlyList<ValidationFailure> failures,
        ValiantSeverity severity)
    {
        if (failures.Count == 0)
        {
            return [];
        }

        var filtered = failures.Where(failure => failure.Severity == severity).ToArray();
        return filtered;
    }
}
