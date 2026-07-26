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
            var revealSecrets = options.AllowSecretReveal &&
                string.Equals(httpContext.Request.Query["revealSecrets"], "true", StringComparison.OrdinalIgnoreCase);

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
