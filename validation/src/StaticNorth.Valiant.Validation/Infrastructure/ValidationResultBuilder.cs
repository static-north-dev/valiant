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
    private ValidationResultAccumulator _accumulator;

    /// <summary>
    /// Initializes a validation result builder.
    /// </summary>
    /// <param name="expectedCapacity">The expected maximum number of failures.</param>
    public ValidationResultBuilder(int expectedCapacity)
    {
        _accumulator = new ValidationResultAccumulator(expectedCapacity);
    }

    /// <summary>
    /// Gets the number of collected failures.
    /// </summary>
    public int Count => _accumulator.Count;

    /// <summary>
    /// Adds a validation failure.
    /// </summary>
    /// <param name="failure">The failure to add.</param>
    public void Add(ValidationFailure failure) => _accumulator.Add(failure);

    /// <summary>
    /// Adds validation failures.
    /// </summary>
    /// <param name="failures">The failures to add.</param>
    public void AddRange(IEnumerable<ValidationFailure> failures) => _accumulator.AddRange(failures);

    /// <summary>
    /// Creates an immutable validation result from the collected failures.
    /// </summary>
    /// <returns>The validation result.</returns>
    public ValidationResult Build() => _accumulator.Build();
}
