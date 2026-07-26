using System.Collections.Generic;

namespace StaticNorth.Valiant.Settings;

/// <summary>
/// Describes a generated settings property schema.
/// </summary>
public sealed class ValiantSettingsPropertySchema
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValiantSettingsPropertySchema"/> class.
    /// </summary>
    public ValiantSettingsPropertySchema(
        string name,
        string configurationKey,
        string typeName,
        ValiantSettingsPropertyKind kind,
        bool isRequired,
        bool isNullable,
        bool isSecret = false,
        string? defaultValue = null,
        ValiantSettingsPropertySchema? item = null,
        IReadOnlyList<ValiantSettingsPropertySchema>? properties = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ConfigurationKey = configurationKey ?? throw new ArgumentNullException(nameof(configurationKey));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        Kind = kind;
        IsRequired = isRequired;
        IsNullable = isNullable;
        IsSecret = isSecret;
        DefaultValue = defaultValue;
        Item = item;
        Properties = properties ?? Array.Empty<ValiantSettingsPropertySchema>();
    }

    /// <summary>
    /// Gets the CLR property name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the configuration key segment for the property.
    /// </summary>
    public string ConfigurationKey { get; }

    /// <summary>
    /// Gets the display name of the CLR property type.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the schema kind of the property.
    /// </summary>
    public ValiantSettingsPropertyKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether the property must be present.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets a value indicating whether the property type allows null.
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Gets a value indicating whether runtime values for the property should be hidden.
    /// </summary>
    public bool IsSecret { get; }

    /// <summary>
    /// Gets the generated default value representation, when one is known.
    /// </summary>
    public string? DefaultValue { get; }

    /// <summary>
    /// Gets the generated item schema for array or collection properties.
    /// </summary>
    public ValiantSettingsPropertySchema? Item { get; }

    /// <summary>
    /// Gets nested property schemas for object properties.
    /// </summary>
    public IReadOnlyList<ValiantSettingsPropertySchema> Properties { get; }
}
