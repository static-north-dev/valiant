using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace StaticNorth.Valiant.Validation.MediatR;

/// <summary>
/// Registration helpers for Valiant validation in MediatR pipelines.
/// </summary>
public static class ValiantMediatRExtensions
{
    /// <summary>
    /// Adds the Valiant validation pipeline behavior to MediatR.
    /// </summary>
    /// <param name="configuration">The MediatR service configuration.</param>
    /// <returns>The MediatR service configuration.</returns>
    public static MediatRServiceConfiguration AddValiantValidation(this MediatRServiceConfiguration configuration)
    {
        _ = configuration ?? throw new ArgumentNullException(nameof(configuration));
        configuration.AddOpenBehavior(typeof(ValiantValidationBehavior<,>));
        return configuration;
    }
}
