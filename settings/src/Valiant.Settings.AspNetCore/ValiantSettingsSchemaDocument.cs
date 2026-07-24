namespace Valiant.Settings.AspNetCore;

/// <summary>
/// Describes generated Valiant settings schemas for UI and JSON consumers.
/// </summary>
public sealed class ValiantSettingsSchemaDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValiantSettingsSchemaDocument"/> class.
    /// </summary>
    public ValiantSettingsSchemaDocument(
        IReadOnlyList<ValiantSettingsSchemaEntry> schemas,
        bool canRevealSecrets = false,
        bool secretsRevealed = false)
    {
        Schemas = schemas ?? throw new ArgumentNullException(nameof(schemas));
        CanRevealSecrets = canRevealSecrets;
        SecretsRevealed = secretsRevealed;
    }

    /// <summary>
    /// Gets the generated settings schemas.
    /// </summary>
    public IReadOnlyList<ValiantSettingsSchemaEntry> Schemas { get; }

    /// <summary>
    /// Gets a value indicating whether this UI endpoint allows requesting unmasked secret values.
    /// </summary>
    public bool CanRevealSecrets { get; }

    /// <summary>
    /// Gets a value indicating whether secret runtime values are currently unmasked.
    /// </summary>
    public bool SecretsRevealed { get; }
}
