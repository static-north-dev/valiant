namespace StaticNorth.Valiant.Mapping;

/// <summary>
/// Defines how mapping rules handle null source values.
/// </summary>
public enum ValiantNullHandling
{
    /// <summary>
    /// Assigns null source values to target members.
    /// </summary>
    MapNulls = 0,

    /// <summary>
    /// Leaves target members at their existing value when the source value is null.
    /// </summary>
    IgnoreSourceNulls = 1
}
