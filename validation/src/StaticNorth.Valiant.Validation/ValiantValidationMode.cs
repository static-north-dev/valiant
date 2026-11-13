namespace StaticNorth.Valiant.Validation;

/// <summary>
/// Selects how eligible fluent validators execute.
/// </summary>
/// <remarks>Attribute validators always use generated checks. Other fluent validators always execute Configure.</remarks>
public enum ValiantValidationMode
{
    /// <summary>
    /// Uses generated checks for eligible fluent validators in Debug and Release builds.
    /// </summary>
    Auto,

    /// <summary>
    /// Executes the original Configure method for fluent validators so breakpoints can bind.
    /// </summary>
    Debuggable,

    /// <summary>
    /// Uses generated checks for eligible fluent validators.
    /// </summary>
    Fast
}
