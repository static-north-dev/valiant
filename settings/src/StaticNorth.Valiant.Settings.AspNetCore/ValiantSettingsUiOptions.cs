namespace StaticNorth.Valiant.Settings.AspNetCore;

/// <summary>
/// Configures the built-in Valiant settings UI endpoint.
/// </summary>
public sealed class ValiantSettingsUiOptions
{
    /// <summary>
    /// The default route prefix for the Valiant settings UI.
    /// </summary>
    public const string DefaultPath = "/_valiant/settings";

    /// <summary>
    /// Gets or sets the route prefix for the Valiant settings UI.
    /// </summary>
    public string Path { get; set; } = DefaultPath;

    /// <summary>
    /// Gets or sets a value indicating whether the schema endpoint should include current runtime settings values.
    /// </summary>
    public bool ShowRuntimeValues { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the UI may request unmasked secret runtime values.
    /// </summary>
    public bool AllowSecretReveal { get; set; }
}
