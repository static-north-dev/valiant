namespace StaticNorth.Valiant.Endpoints.AspNetCore.Samples.Basic;

[ValiantEndpointGroup]
public class PrivateEndpointGroup : IEndpointGroup
{
    public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) =>
        builder.MapGroup("/private");
}
