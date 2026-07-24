namespace Valiant.Endpoints.AspNetCore.Samples.ConditionalRegistration;

[ValiantEndpoint]
public sealed class BlockedStandaloneEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet(
            "/conditional/endpoints/blocked",
            () => Results.Ok("This route is not mapped."));
}
