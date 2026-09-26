using System.Runtime.CompilerServices;

namespace StaticNorth.Valiant.Validation;

/// <summary>
/// Describes a single validation failure.
/// </summary>
public readonly struct ValidationFailure
{
    /// <summary>
    /// Initializes a validation failure.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValidationFailure(
        string propertyName,
        string errorCode,
        string errorMessage,
        ValiantSeverity severity = ValiantSeverity.Error)
    {
        PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
        ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
        Severity = severity;
    }

    /// <summary>
    /// Gets the property path that failed validation.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the stable machine-readable error code.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the human-readable validation message.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// Gets the validation failure severity.
    /// </summary>
    public ValiantSeverity Severity { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return ErrorMessage;
    }
}
