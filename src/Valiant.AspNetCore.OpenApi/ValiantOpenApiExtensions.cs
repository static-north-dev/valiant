using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Valiant.AspNetCore.OpenApi;

/// <summary>
/// Registration helpers for Valiant native OpenAPI integration.
/// </summary>
public static class ValiantOpenApiExtensions
{
    /// <summary>
    /// Adds Valiant validation rule reflection to native ASP.NET Core OpenAPI generation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddValiantOpenApi(this IServiceCollection services)
    {
        _ = services ?? throw new ArgumentNullException(nameof(services));

        services.TryAddSingleton<ValiantValidationSchemaTransformer>();
        services.ConfigureAll<OpenApiOptions>(static options => options.AddValiantValidationRules());
        return services;
    }

    /// <summary>
    /// Adds the Valiant schema transformer to native ASP.NET Core OpenAPI options.
    /// </summary>
    /// <param name="options">The OpenAPI options.</param>
    /// <returns>The OpenAPI options.</returns>
    public static OpenApiOptions AddValiantValidationRules(this OpenApiOptions options)
    {
        _ = options ?? throw new ArgumentNullException(nameof(options));

        options.AddSchemaTransformer<ValiantValidationSchemaTransformer>();
        return options;
    }
}
