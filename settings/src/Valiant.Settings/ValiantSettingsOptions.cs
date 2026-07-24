namespace Valiant.Settings;

/// <summary>
/// Configures generated Valiant settings registration.
/// </summary>
public class ValiantSettingsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether generated settings schema providers should be registered in dependency injection.
    /// </summary>
    public bool RegisterSchemaProviders { get; set; } = true;
}
