namespace StaticNorth.Valiant.Validation;

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
        _ = errors ?? throw new ArgumentNullException(nameof(errors));
        if (errors.Count == 0)
        {
            Failures = [];
            Errors = [];
            Warnings = [];
            Infos = [];
            return;
        }

        var failures = new ValidationFailure[errors.Count];
        List<ValidationFailure>? validationErrors = null;
        List<ValidationFailure>? warnings = null;
        List<ValidationFailure>? infos = null;

        for (var index = 0; index < errors.Count; index++)
        {
            var failure = errors[index];
            failures[index] = failure;
            switch (failure.Severity)
            {
                case ValiantSeverity.Warning:
                    (warnings ??= []).Add(failure);
                    break;
                case ValiantSeverity.Info:
                    (infos ??= []).Add(failure);
                    break;
                default:
                    (validationErrors ??= []).Add(failure);
                    break;
            }
        }

        Failures = failures;
        Errors = ToReadOnlyArray(validationErrors);
        Warnings = ToReadOnlyArray(warnings);
        Infos = ToReadOnlyArray(infos);
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

    private static IReadOnlyList<ValidationFailure> ToReadOnlyArray(List<ValidationFailure>? failures)
    {
        return failures is null ? [] : failures.ToArray();
    }
}
