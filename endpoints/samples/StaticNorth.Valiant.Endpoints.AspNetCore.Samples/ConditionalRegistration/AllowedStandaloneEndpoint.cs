namespace Valiant.Endpoints.AspNetCore.Samples.ConditionalRegistration;

[ValiantEndpoint]
public sealed class AllowedStandaloneEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet(
            "/conditional/endpoints/allowed",
            () => Results.Ok("The standalone endpoint was allowed."));
}
