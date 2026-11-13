using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace StaticNorth.Valiant.Settings.AspNetCore;

/// <summary>
/// Maps ASP.NET Core endpoints for visualizing generated Valiant settings schemas.
/// </summary>
public static class ValiantSettingsUiEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Valiant settings schema UI.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route prefix for the UI. Defaults to <c>/_valiant/settings</c>.</param>
    /// <returns>The mapped route group.</returns>
    public static RouteGroupBuilder MapValiantSettingsUi(
        this IEndpointRouteBuilder endpoints,
        string pattern = ValiantSettingsUiOptions.DefaultPath)
    {
        _ = endpoints ?? throw new ArgumentNullException(nameof(endpoints));

        var options = new ValiantSettingsUiOptions
        {
            Path = pattern
        };

        return MapValiantSettingsUiCore(endpoints, options);
    }

    private static RouteGroupBuilder MapValiantSettingsUiCore(
        IEndpointRouteBuilder endpoints,
        ValiantSettingsUiOptions options)
    {
        var group = endpoints.MapGroup(NormalizePattern(options.Path));
        group.MapGet("", static () => Results.Content(ValiantSettingsUiPage.Html, "text/html; charset=utf-8"));
        group.MapGet("/schemas", (HttpContext httpContext, IEnumerable<IValiantSettingsSchemaProvider> providers, IServiceProvider serviceProvider) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            var revealSecrets = options.AllowSecretReveal &&
                string.Equals(httpContext.Request.Query["revealSecrets"], "true", StringComparison.OrdinalIgnoreCase);

            if (httpContext.Request.Query.TryGetValue("schemaType", out var schemaType))
            {
                if (!options.ShowRuntimeValues)
                {
                    return Results.NotFound();
                }

                var section = httpContext.Request.Query["section"].ToString();
                var name = httpContext.Request.Query["name"].ToString();
                var schema = providers.Select(static provider => provider.Schema).FirstOrDefault(candidate =>
                    string.Equals(candidate.SettingsType.FullName ?? candidate.SettingsType.Name, schemaType, StringComparison.Ordinal) &&
                    string.Equals(candidate.SectionPath ?? string.Empty, section, StringComparison.Ordinal) &&
                    string.Equals(candidate.Name, name, StringComparison.Ordinal));
                if (schema is null)
                {
                    return Results.NotFound();
                }

                var runtimeValues = ValiantSettingsRuntimeValueFactory.TryCreate(schema, serviceProvider, revealSecrets, out var runtimeValuesError);
                return Results.Json(new { runtimeValues, runtimeValuesError, secretsRevealed = revealSecrets });
            }

            if (httpContext.Request.Query["schemaOnly"] == "true")
            {
                return Results.Json(new
                {
                    ValiantSettingsSchemaDocumentFactory.Create(providers).Schemas,
                    options.ShowRuntimeValues,
                    CanRevealSecrets = options.ShowRuntimeValues && options.AllowSecretReveal
                });
            }

            return Results.Json(ValiantSettingsSchemaDocumentFactory.Create(
                providers,
                serviceProvider,
                options.ShowRuntimeValues,
                revealSecrets,
                options.AllowSecretReveal));
        });

        return group;
    }

    /// <summary>
    /// Maps the Valiant settings schema UI with configured options.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Configures the settings UI endpoint.</param>
    /// <returns>The mapped route group.</returns>
    public static RouteGroupBuilder MapValiantSettingsUi(
        this IEndpointRouteBuilder endpoints,
        Action<ValiantSettingsUiOptions> configure)
    {
        _ = configure ?? throw new ArgumentNullException(nameof(configure));

        var options = new ValiantSettingsUiOptions();
        configure(options);

        return MapValiantSettingsUiCore(endpoints, options);
    }

    private static string NormalizePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("The Valiant settings UI route pattern cannot be empty.", nameof(pattern));
        }

        var normalized = pattern.StartsWith('/')
            ? pattern
            : "/" + pattern;

        return normalized.Length > 1
            ? normalized.TrimEnd('/')
            : normalized;
    }
}
