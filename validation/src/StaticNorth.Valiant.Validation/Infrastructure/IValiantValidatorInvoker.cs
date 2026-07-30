using System.ComponentModel;

namespace StaticNorth.Valiant.Validation.Infrastructure;

/// <summary>
/// Provides the generated, strongly typed bridge used to invoke validators without runtime generic construction.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IValiantValidatorInvoker
{
    /// <summary>
    /// Gets the exact model type handled by this invoker.
    /// </summary>
    Type ModelType { get; }

    /// <summary>
    /// Resolves and snapshots validators for the current request.
    /// </summary>
    /// <param name="services">The request service provider.</param>
    /// <returns>An opaque validator snapshot for <see cref="ValidateAsync" />.</returns>
    object ResolveValidators(IServiceProvider services);

    /// <summary>
    /// Validates a model with a previously resolved validator snapshot.
    /// </summary>
    /// <param name="validators">The validator snapshot returned by <see cref="ResolveValidators" />.</param>
    /// <param name="model">The model to validate.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The combined validation result.</returns>
    ValueTask<ValidationResult> ValidateAsync(
        object validators,
        object model,
        CancellationToken cancellationToken);
}
