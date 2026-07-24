using System;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Valiant.Validation.Mediator;

/// <summary>
/// Registration helpers for Valiant validation in source-generated Mediator pipelines.
/// </summary>
public static class ValiantMediatorServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Valiant validation pipeline behavior to Mediator through dependency injection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddValiantMediatorValidation(this IServiceCollection services)
    {
        _ = services ?? throw new ArgumentNullException(nameof(services));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), typeof(ValiantValidationBehavior<,>)));
        return services;
    }
}
