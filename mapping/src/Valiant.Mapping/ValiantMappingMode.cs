namespace Valiant.Mapping;

/// <summary>
/// Selects which mapping backend generated mappers should use.
/// </summary>
public enum ValiantMappingMode
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
    /// Uses the source-generated direct mapping implementation.
    /// </summary>
    Fast
}
