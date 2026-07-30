using System.ComponentModel;

namespace StaticNorth.Valiant.Validation.Infrastructure;

/// <summary>
/// Collects validation failures for generated validation code.
/// </summary>
/// <remarks>
/// This type is infrastructure used by generated code and is not intended for direct application use.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ValidationResultBuilder
{
    private readonly int _expectedCapacity;
    private ValidationFailure _firstFailure;
    private ValidationFailure[]? _failures;
    private ValidationResult? _result;
    private int _count;
    private int _errorCount;
    private int _warningCount;
    private int _infoCount;

    /// <summary>
    /// Initializes a validation result builder.
    /// </summary>
    /// <param name="expectedCapacity">The expected maximum number of failures.</param>
    public ValidationResultBuilder(int expectedCapacity)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(expectedCapacity);
#else
        if (expectedCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCapacity));
        }
#endif

        _expectedCapacity = expectedCapacity;
    }

    /// <summary>
    /// Gets the number of collected failures.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Adds a validation failure.
    /// </summary>
    /// <param name="failure">The failure to add.</param>
    public void Add(ValidationFailure failure)
    {
        ThrowIfBuilt();

        if (_count == 0)
        {
            _firstFailure = failure;
        }
        else
        {
            EnsureFailureBuffer();
            _failures![_count] = failure;
        }

        _count++;
        switch (failure.Severity)
        {
            case ValiantSeverity.Warning:
                _warningCount++;
                break;
            case ValiantSeverity.Info:
                _infoCount++;
                break;
            default:
                _errorCount++;
                break;
        }
    }

    /// <summary>
    /// Adds validation failures.
    /// </summary>
    /// <param name="failures">The failures to add.</param>
    public void AddRange(IEnumerable<ValidationFailure> failures)
    {
        _ = failures ?? throw new ArgumentNullException(nameof(failures));

        foreach (var failure in failures)
        {
            Add(failure);
        }
    }

    /// <summary>
    /// Creates an immutable validation result from the collected failures.
    /// </summary>
    /// <returns>The validation result.</returns>
    public ValidationResult Build()
    {
        if (_result is not null)
        {
            return _result;
        }

        if (_count == 0)
        {
            _result = ValidationResult.Valid;
            return _result;
        }

        ValidationFailure[] failures;
        if (_failures is null)
        {
            failures = [_firstFailure];
        }
        else if (_failures.Length == _count)
        {
            failures = _failures;
        }
        else
        {
            failures = new ValidationFailure[_count];
            Array.Copy(_failures, failures, _count);
        }

        _result = ValidationResult.CreateOwned(
            failures,
            _errorCount,
            _warningCount,
            _infoCount);
        return _result;
    }

    private void EnsureFailureBuffer()
    {
        if (_failures is null)
        {
            var capacity = Math.Max(2, _expectedCapacity);
            _failures = new ValidationFailure[capacity];
            _failures[0] = _firstFailure;
            return;
        }

        if (_count < _failures.Length)
        {
            return;
        }

        if (_failures.Length > int.MaxValue / 2)
        {
            throw new InvalidOperationException("The validation result contains too many failures.");
        }

        var newCapacity = _failures.Length * 2;
        Array.Resize(ref _failures, newCapacity);
    }

    private void ThrowIfBuilt()
    {
        if (_result is not null)
        {
            throw new InvalidOperationException("Failures cannot be added after the validation result has been built.");
        }
    }
}
