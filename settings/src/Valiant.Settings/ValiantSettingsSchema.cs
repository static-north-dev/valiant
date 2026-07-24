using System.Collections.Generic;

namespace Valiant.Settings;

/// <summary>
/// Describes a generated settings model schema.
/// </summary>
public sealed class ValiantSettingsSchema
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValiantSettingsSchema"/> class.
    /// </summary>
    public ValiantSettingsSchema(
        Type settingsType,
        string name,
        string? sectionPath,
        IReadOnlyList<ValiantSettingsPropertySchema> properties)
    {
        SettingsType = settingsType ?? throw new ArgumentNullException(nameof(settingsType));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SectionPath = sectionPath;
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
    }

    /// <summary>
    /// Gets the CLR settings model type.
    /// </summary>
    public Type SettingsType { get; }

    /// <summary>
    /// Gets the settings model display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the optional configuration section path associated with the settings model.
    /// </summary>
    public string? SectionPath { get; }

    /// <summary>
    /// Gets the generated property schemas for the settings model.
    /// </summary>
    public IReadOnlyList<ValiantSettingsPropertySchema> Properties { get; }
}
