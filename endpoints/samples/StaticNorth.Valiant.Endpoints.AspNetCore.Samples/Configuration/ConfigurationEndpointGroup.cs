namespace StaticNorth.Valiant.Endpoints.AspNetCore.Samples.Configuration;

[ValiantEndpointGroup]
public sealed class ConfigurationEndpointGroup : IEndpointGroup
{
    public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) =>
        builder.MapGroup("/configuration");
}
