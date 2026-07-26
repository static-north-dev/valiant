namespace StaticNorth.Valiant.Validation;

/// <summary>
/// Describes how severe a validation failure is.
/// </summary>
public enum ValiantSeverity
{
    /// <summary>
    /// The failure blocks the model from being considered valid.
    /// </summary>
    Error,

    /// <summary>
    /// The failure should be reported, but does not make the model invalid.
    /// </summary>
    Warning,

    /// <summary>
    /// The failure is informational and does not make the model invalid.
    /// </summary>
    Info
}
