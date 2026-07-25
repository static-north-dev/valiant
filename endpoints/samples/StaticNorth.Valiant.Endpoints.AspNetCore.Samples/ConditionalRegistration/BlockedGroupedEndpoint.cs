namespace Valiant.Endpoints.AspNetCore.Samples.ConditionalRegistration;

[ValiantEndpoint<BlockedEndpointGroup>]
public sealed class BlockedGroupedEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet("/hello", () => Results.Ok("This route is not mapped."));
}
