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
        var errorCount = 0;
        var warningCount = 0;
        var infoCount = 0;

        for (var index = 0; index < errors.Count; index++)
        {
            var failure = errors[index];
            failures[index] = failure;
            switch (failure.Severity)
            {
                case ValiantSeverity.Warning:
                    warningCount++;
                    break;
                case ValiantSeverity.Info:
                    infoCount++;
                    break;
                default:
                    errorCount++;
                    break;
            }
        }

        Initialize(failures, errorCount, warningCount, infoCount);
    }

    private ValidationResult(
        ValidationFailure[] failures,
        int errorCount,
        int warningCount,
        int infoCount)
    {
        Initialize(failures, errorCount, warningCount, infoCount);
    }

    /// <summary>
    /// Gets a value indicating whether validation succeeded.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets every validation failure, including warnings and informational failures.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Failures { get; private set; } = [];

    /// <summary>
    /// Gets validation failures with <see cref="ValiantSeverity.Error" /> severity.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Errors { get; private set; } = [];

    /// <summary>
    /// Gets validation failures with <see cref="ValiantSeverity.Warning" /> severity.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Warnings { get; private set; } = [];

    /// <summary>
    /// Gets validation failures with <see cref="ValiantSeverity.Info" /> severity.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Infos { get; private set; } = [];

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

    internal static ValidationResult CreateOwned(
        ValidationFailure[] failures,
        int errorCount,
        int warningCount,
        int infoCount)
    {
        _ = failures ?? throw new ArgumentNullException(nameof(failures));
        if (errorCount < 0 ||
            warningCount < 0 ||
            infoCount < 0 ||
            errorCount + warningCount + infoCount != failures.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(errorCount));
        }

        return failures.Length == 0
            ? Valid
            : new ValidationResult(failures, errorCount, warningCount, infoCount);
    }

    private void Initialize(
        ValidationFailure[] failures,
        int errorCount,
        int warningCount,
        int infoCount)
    {
        var readOnlyFailures = Array.AsReadOnly(failures);
        if (errorCount == failures.Length)
        {
            Failures = readOnlyFailures;
            Errors = readOnlyFailures;
            Warnings = [];
            Infos = [];
            return;
        }

        if (warningCount == failures.Length)
        {
            Failures = readOnlyFailures;
            Errors = [];
            Warnings = readOnlyFailures;
            Infos = [];
            return;
        }

        if (infoCount == failures.Length)
        {
            Failures = readOnlyFailures;
            Errors = [];
            Warnings = [];
            Infos = readOnlyFailures;
            return;
        }

        var indexes = new int[failures.Length];
        var errorIndex = 0;
        var warningIndex = errorCount;
        var infoIndex = errorCount + warningCount;

        for (var index = 0; index < failures.Length; index++)
        {
            var failure = failures[index];
            switch (failure.Severity)
            {
                case ValiantSeverity.Warning:
                    indexes[warningIndex++] = index;
                    break;
                case ValiantSeverity.Info:
                    indexes[infoIndex++] = index;
                    break;
                default:
                    indexes[errorIndex++] = index;
                    break;
            }
        }

        Failures = readOnlyFailures;
        Errors = errorCount == 0
            ? []
            : new ValidationFailurePartition(failures, indexes, 0, errorCount);
        Warnings = warningCount == 0
            ? []
            : new ValidationFailurePartition(failures, indexes, errorCount, warningCount);
        Infos = infoCount == 0
            ? []
            : new ValidationFailurePartition(failures, indexes, errorCount + warningCount, infoCount);
    }

    private sealed class ValidationFailurePartition(
        ValidationFailure[] failures,
        int[] indexes,
        int offset,
        int count) : IReadOnlyList<ValidationFailure>
    {
        public int Count { get; } = count;

        public ValidationFailure this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return failures[indexes[offset + index]];
            }
        }

        public IEnumerator<ValidationFailure> GetEnumerator()
        {
            for (var index = 0; index < Count; index++)
            {
                yield return failures[indexes[offset + index]];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
