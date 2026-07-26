namespace StaticNorth.Valiant.Endpoints.AspNetCore.Samples.Configuration;

[ValiantEndpoint<ConfigurationEndpointGroup>]
public sealed class GroupOnlyEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet(
            "/group-only",
            () => Results.Ok(new
            {
                Message = "This endpoint receives only group configuration."
            }));
}
