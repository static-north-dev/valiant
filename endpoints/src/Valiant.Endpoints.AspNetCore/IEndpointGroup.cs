using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Marks a type as a Valiant endpoint group.
/// </summary>
/// <remarks>
/// Endpoint groups implement <c>CreateGroup</c> to choose the route group, route prefix,
/// and group-level conventions. Source-generated endpoints that reference this group are
/// mapped into the returned group automatically.
/// </remarks>
public interface IEndpointGroup
{
    /// <summary>
    /// Creates the route group and applies group-level conventions such as tags, authorization, filters, or OpenAPI metadata.
    /// </summary>
    /// <param name="builder">The route builder to create the group on.</param>
    /// <returns>The configured route group builder.</returns>
    static abstract RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder);
}
