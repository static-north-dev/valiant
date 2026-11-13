using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using StaticNorth.Valiant.Endpoints;
using StaticNorth.Valiant.Endpoints.AspNetCore;
using StaticNorth.Valiant.Validation.AspNetCore;

namespace StaticNorth.Valiant.Validation.Tests;

[ValiantEndpoint]
public sealed class UnionEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        => builder.MapPost("/union", Handle).WithValiantValidation();

    private static Ok<UnionEndpointPayload> Handle(UnionEndpointPayload payload) => TypedResults.Ok(payload);
}
