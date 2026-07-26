namespace StaticNorth.Valiant.Endpoints.AspNetCore.Samples.ConditionalRegistration;

[ValiantEndpointGroup]
public sealed class BlockedEndpointGroup : IEndpointGroup
{
    public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) =>
        builder.MapGroup("/conditional/groups/blocked");
}
