namespace Valiant.Validation;

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
    /// Gets or sets whether validators collect all failures or stop after the first failure.
    /// </summary>
    public ValiantFailureBehavior FailureBehavior { get; set; } = ValiantFailureBehavior.ValidateAll;

    /// <summary>
    /// Gets or sets the dependency injection lifetime for generated validators.
    /// </summary>
    public ValiantValidatorLifetime Lifetime { get; set; } = ValiantValidatorLifetime.Scoped;
}
