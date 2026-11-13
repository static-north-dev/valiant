namespace StaticNorth.Valiant.Mapping;

/// <summary>
/// Selects which mapping backend generated mappers should use.
/// </summary>
public enum ValiantMappingMode
{
    /// <summary>
    /// Uses the source-generated direct mapping implementation in Debug and Release builds.
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
