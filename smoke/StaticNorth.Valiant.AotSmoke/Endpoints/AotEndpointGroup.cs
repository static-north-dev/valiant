using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using StaticNorth.Valiant.Endpoints;
using StaticNorth.Valiant.Endpoints.AspNetCore;

[ValiantEndpointGroup]
internal sealed class AotEndpointGroup : IEndpointGroup
{
    public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) => builder.MapGroup("/aot");
}
