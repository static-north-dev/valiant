namespace StaticNorth.Valiant.Endpoints.AspNetCore.Samples.ConditionalRegistration;

[ValiantEndpoint<AllowedEndpointGroup>]
public sealed class AllowedGroupedEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet("/hello", () => Results.Ok("The endpoint group was allowed."));
}
