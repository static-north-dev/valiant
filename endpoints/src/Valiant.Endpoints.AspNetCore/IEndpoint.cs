using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Defines a single Minimal API endpoint.
/// </summary>
public interface IEndpoint
{
    /// <summary>
    /// Maps the endpoint to the supplied route builder.
    /// </summary>
    /// <param name="builder">The route builder to map the endpoint on.</param>
    /// <returns>The mapped endpoint convention builder.</returns>
    static abstract IEndpointConventionBuilder Map(IEndpointRouteBuilder builder);
}
