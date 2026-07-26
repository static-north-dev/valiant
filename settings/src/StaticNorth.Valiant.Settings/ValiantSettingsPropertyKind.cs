namespace StaticNorth.Valiant.Settings;

/// <summary>
/// Describes the schema kind of a settings property.
/// </summary>
public enum ValiantSettingsPropertyKind
{
    /// <summary>
    /// The property kind is not known.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A string value.
    /// </summary>
    String,

    /// <summary>
    /// A Boolean value.
    /// </summary>
    Boolean,

    /// <summary>
    /// An integral numeric value.
    /// </summary>
    Integer,

    /// <summary>
    /// A floating point or decimal numeric value.
    /// </summary>
    Number,

    /// <summary>
    /// An enum value.
    /// </summary>
    Enum,

    /// <summary>
    /// A nested object value.
    /// </summary>
    Object,

    /// <summary>
    /// An array or collection value.
    /// </summary>
    Array
}
