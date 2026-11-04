using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using StaticNorth.Valiant.Endpoints;
using StaticNorth.Valiant.Endpoints.AspNetCore;

[ValiantEndpoint]
internal sealed class AotStandaloneEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => builder.MapGet("/generated", Handle);

    private static ContentHttpResult Handle() => TypedResults.Text("standalone");
}
