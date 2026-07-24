namespace Valiant.Settings.AspNetCore;

using System.Text.Json.Serialization;

/// <summary>
/// Describes one generated settings model schema.
/// </summary>
public sealed class ValiantSettingsSchemaEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValiantSettingsSchemaEntry"/> class.
    /// </summary>
    public ValiantSettingsSchemaEntry(
        string name,
        string typeName,
        string? sectionPath,
        IReadOnlyList<ValiantSettingsSchemaPropertyEntry> properties,
        object? runtimeValues = null,
        string? runtimeValuesError = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        SectionPath = sectionPath;
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        RuntimeValues = runtimeValues;
        RuntimeValuesError = runtimeValuesError;
    }

    /// <summary>
    /// Gets the settings model name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the settings model CLR type name.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the configuration section path, when one was declared.
    /// </summary>
    public string? SectionPath { get; }

    /// <summary>
    /// Gets the model property schemas.
    /// </summary>
    public IReadOnlyList<ValiantSettingsSchemaPropertyEntry> Properties { get; }

    /// <summary>
    /// Gets the current runtime settings values, when enabled for the UI endpoint.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? RuntimeValues { get; }

    /// <summary>
    /// Gets the runtime value read error, when values were requested but could not be resolved.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RuntimeValuesError { get; }
}
