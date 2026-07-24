using System.ComponentModel;
using Microsoft.AspNetCore.Routing;

namespace Valiant.Endpoints.AspNetCore.Infrastructure;

/// <summary>
/// Infrastructure contract used by source-generated endpoint registrars.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IValiantEndpointRouteRegistrar
{
    /// <summary>
    /// Maps endpoints to the supplied route builder.
    /// </summary>
    /// <param name="builder">The route builder to map endpoints on.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    void MapEndpoints(IEndpointRouteBuilder builder);
}
