namespace StaticNorth.Valiant.Settings.Validation;

using StaticNorth.Valiant.Settings;

/// <summary>
/// Configures Valiant validation integration for generated settings options.
/// </summary>
public sealed class ValiantSettingsValidationOptions : ValiantSettingsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether generated settings registrations should add
    /// <see cref="Microsoft.Extensions.Options.IValidateOptions{TOptions}" /> adapters backed by Valiant validators.
    /// </summary>
    public bool ValidateWithValiant { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether validation should fail when no matching
    /// <c>IValidator&lt;TSettings&gt;</c> is registered for a settings model.
    /// </summary>
    public bool RequireValidators { get; set; }
}
