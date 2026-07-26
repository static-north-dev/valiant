namespace StaticNorth.Valiant.Settings;

/// <summary>
/// Marks a type as a Valiant settings model for source-generated schema metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ValiantSettingsAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValiantSettingsAttribute"/> class.
    /// </summary>
    /// <param name="sectionPath">The configuration section path for the settings model.</param>
    /// <param name="configure">Whether generated dependency injection registration should bind this model to the section.</param>
    public ValiantSettingsAttribute(string sectionPath, bool configure = false)
    {
        SectionPath = string.IsNullOrWhiteSpace(sectionPath)
            ? throw new ArgumentException("The settings section path cannot be empty.", nameof(sectionPath))
            : sectionPath;
        Configure = configure;
    }

    /// <summary>
    /// Gets the configuration section path for the settings model.
    /// </summary>
    public string SectionPath { get; }

    /// <summary>
    /// Gets a value indicating whether generated dependency injection registration should bind this model to the section.
    /// </summary>
    public bool Configure { get; }
}
