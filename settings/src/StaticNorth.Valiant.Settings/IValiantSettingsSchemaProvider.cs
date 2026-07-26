namespace StaticNorth.Valiant.Settings;

/// <summary>
/// Provides generated schema metadata for a Valiant settings model.
/// </summary>
public interface IValiantSettingsSchemaProvider
{
    /// <summary>
    /// Gets the settings model type described by the schema.
    /// </summary>
    Type SettingsType { get; }

    /// <summary>
    /// Gets the optional configuration section path associated with the settings model.
    /// </summary>
    string? SectionPath { get; }

    /// <summary>
    /// Gets the generated schema metadata for the settings model.
    /// </summary>
    ValiantSettingsSchema Schema { get; }
}
