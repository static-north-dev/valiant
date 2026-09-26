using System.ComponentModel;
using System.Threading;

namespace StaticNorth.Valiant.Validation.Infrastructure;

/// <summary>
/// Reuses immutable results for a small, fixed set of generated failures.
/// </summary>
/// <remarks>
/// This type is infrastructure used by generated code and is not intended for direct application use.
/// Only failure content is cached; validation checks must run on every invocation.
/// Results are created lazily and retained for at most 63 nonempty failure combinations.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ValidationResultCache
{
    private readonly ValidationFailure[] _failures;
    private readonly ValidationResult?[] _results;

    /// <summary>
    /// Initializes a cache from fixed failures in validation order.
    /// </summary>
    /// <param name="failures">Between one and six fixed failures.</param>
    public ValidationResultCache(ValidationFailure[] failures)
    {
        _ = failures ?? throw new ArgumentNullException(nameof(failures));
        if (failures.Length is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(failures));
        }

        _failures = (ValidationFailure[])failures.Clone();
        _results = new ValidationResult?[1 << failures.Length];
    }

    /// <summary>
    /// Gets an immutable result for the specified failure combination.
    /// </summary>
    /// <param name="failureMask">A bit for each failed check, in validation order.</param>
    /// <returns>The shared immutable result.</returns>
    public ValidationResult Get(int failureMask)
    {
        if ((uint)failureMask >= (uint)_results.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(failureMask));
        }

        if (failureMask == 0)
        {
            return ValidationResult.Valid;
        }

        var result = Volatile.Read(ref _results[failureMask]);
        if (result is not null)
        {
            return result;
        }

        return CreateResult(failureMask);
    }

    private ValidationResult CreateResult(int failureMask)
    {
        var count = 0;
        for (var bits = failureMask; bits != 0; bits &= bits - 1)
        {
            count++;
        }

        var accumulator = new ValidationResultAccumulator(count);
        for (var index = 0; index < _failures.Length; index++)
        {
            if ((failureMask & (1 << index)) != 0)
            {
                accumulator.Add(_failures[index]);
            }
        }

        var result = accumulator.Build();
        return Interlocked.CompareExchange(ref _results[failureMask], result, null) ?? result;
    }
}
