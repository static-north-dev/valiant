namespace Valiant.Validation;

/// <summary>
/// Controls whether validation continues after a failure or stops as soon as possible.
/// </summary>
public enum ValiantFailureBehavior
{
    /// <summary>
    /// Collects all validation failures.
    /// </summary>
    ValidateAll,

    /// <summary>
    /// Stops validation after the first failure in the configured scope.
    /// </summary>
    FailFast
}
