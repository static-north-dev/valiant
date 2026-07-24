namespace Valiant.Validation;

/// <summary>
/// Configures runtime behavior for generated Valiant validators.
/// </summary>
/// <remarks>
/// These options are used by validators generated from <see cref="ValiantValidatorAttribute" /> and by
/// dependency injection registration emitted by the Valiant source generator.
/// </remarks>
public sealed class ValiantValidationOptions
{
    /// <summary>
    /// Gets or sets which validation backend generated validators use.
    /// </summary>
    /// <remarks>
    /// The default value is <see cref="ValiantValidationMode.Auto" />, which uses the debuggable runtime
    /// backend in Debug builds and the source-generated fast backend otherwise.
    /// </remarks>
    public ValiantValidationMode Mode { get; set; } = ValiantValidationMode.Auto;

    /// <summary>
    /// Gets or sets whether validators collect all failures or stop after the first failure in the configured scope.
    /// </summary>
    /// <remarks>
    /// The default value is <see cref="ValiantFailureBehavior.ValidateAll" />. This global setting can be
    /// overridden by validator-level and property-chain failure behavior configuration.
    /// </remarks>
    public ValiantFailureBehavior FailureBehavior { get; set; } = ValiantFailureBehavior.ValidateAll;

    /// <summary>
    /// Gets or sets the dependency injection lifetime used when generated validators are registered.
    /// </summary>
    /// <remarks>
    /// The default value is <see cref="ValiantValidatorLifetime.Scoped" />. This option is applied by the
    /// generated <c>AddValiantValidators</c> service collection extension.
    /// </remarks>
    public ValiantValidatorLifetime Lifetime { get; set; } = ValiantValidatorLifetime.Scoped;
}
