namespace Valiant.Validation;

/// <summary>
/// Selects which validation backend generated validators should use.
/// </summary>
public enum ValiantValidationMode
{
    /// <summary>
    /// Uses the debuggable runtime backend in Debug builds and the generated backend otherwise.
    /// </summary>
    Auto,

    /// <summary>
    /// Executes the user-authored Configure method at runtime so breakpoints work normally.
    /// </summary>
    Debuggable,

    /// <summary>
    /// Uses the source-generated direct validation implementation.
    /// </summary>
    Fast
}
