namespace Valiant.Validation;

/// <summary>
/// Controls how generated validators are registered with dependency injection.
/// </summary>
public enum ValiantValidatorLifetime
{
    /// <summary>
    /// Registers validators as singleton services.
    /// </summary>
    Singleton,

    /// <summary>
    /// Registers validators as scoped services.
    /// </summary>
    Scoped,

    /// <summary>
    /// Registers validators as transient services.
    /// </summary>
    Transient
}
