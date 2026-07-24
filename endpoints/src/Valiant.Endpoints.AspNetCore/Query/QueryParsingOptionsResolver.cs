using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Resolves the query parsing options for the current endpoint request.
/// </summary>
internal static class QueryParsingOptionsResolver
{
    /// <summary>
    /// Resolves endpoint-specific query parsing options, falling back to the globally configured options and then defaults.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The query parsing options to use for the request.</returns>
    internal static QueryParsingOptions Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var endpoint = context.GetEndpoint();
        var endpointOptions = endpoint?
            .Metadata
            .GetMetadata<EndpointQueryParsingOptionsMetadata>()?
            .Options;
        var registrationOptions = endpoint?
            .Metadata
            .GetMetadata<EndpointRegistrationQueryParsingOptionsMetadata>()?
            .Options;
        var baseOptions = endpointOptions ?? registrationOptions ?? context.RequestServices
            .GetService<IOptions<ValiantEndpointOptions>>()?
            .Value
            .QueryParameters.Parsing ?? new QueryParsingOptions();
        var overrides = endpoint?
            .Metadata
            .GetMetadata<EndpointQueryParsingOverridesMetadata>();
        return overrides?.ApplyTo(baseOptions) ?? baseOptions;
    }
}
