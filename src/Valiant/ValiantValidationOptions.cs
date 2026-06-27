namespace Valiant;

/// <summary>
/// Configures generated Valiant validators.
/// </summary>
public sealed class ValiantValidationOptions
{
    /// <summary>
    /// Gets or sets the validation backend mode.
    /// </summary>
    public ValiantValidationMode Mode { get; set; } = ValiantValidationMode.Auto;

    /// <summary>
    /// Gets or sets the dependency injection lifetime for generated validators.
    /// </summary>
    public ValiantValidatorLifetime Lifetime { get; set; } = ValiantValidatorLifetime.Singleton;
}
