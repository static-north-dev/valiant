namespace Valiant.Endpoints.AspNetCore.Samples.ConditionalRegistration;

[ValiantEndpointGroup]
public sealed class AllowedEndpointGroup : IEndpointGroup
{
    public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) =>
        builder.MapGroup("/conditional/groups/allowed");
}
