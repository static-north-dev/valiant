namespace StaticNorth.Valiant.Settings.AspNetCore;

/// <summary>
/// Describes one generated settings property schema.
/// </summary>
public sealed class ValiantSettingsSchemaPropertyEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValiantSettingsSchemaPropertyEntry"/> class.
    /// </summary>
    public ValiantSettingsSchemaPropertyEntry(
        string name,
        string configurationKey,
        string typeName,
        string kind,
        bool isRequired,
        bool isNullable,
        bool isSecret,
        string? defaultValue,
        ValiantSettingsSchemaPropertyEntry? item,
        IReadOnlyList<ValiantSettingsSchemaPropertyEntry> properties)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ConfigurationKey = configurationKey ?? throw new ArgumentNullException(nameof(configurationKey));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        IsRequired = isRequired;
        IsNullable = isNullable;
        IsSecret = isSecret;
        DefaultValue = defaultValue;
        Item = item;
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
    }

    /// <summary>
    /// Gets the CLR property name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the configuration key segment.
    /// </summary>
    public string ConfigurationKey { get; }

    /// <summary>
    /// Gets the CLR type name.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the property schema kind.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets a value indicating whether the property is required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets a value indicating whether the property allows null.
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Gets a value indicating whether runtime values for this property should be hidden.
    /// </summary>
    public bool IsSecret { get; }

    /// <summary>
    /// Gets the known default value, when available.
    /// </summary>
    public string? DefaultValue { get; }

    /// <summary>
    /// Gets the item schema for array and collection properties.
    /// </summary>
    public ValiantSettingsSchemaPropertyEntry? Item { get; }

    /// <summary>
    /// Gets nested object property schemas.
    /// </summary>
    public IReadOnlyList<ValiantSettingsSchemaPropertyEntry> Properties { get; }
}
