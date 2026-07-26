namespace StaticNorth.Valiant.Endpoints.AspNetCore.Samples.Configuration;

[ValiantEndpoint<ConfigurationEndpointGroup>]
public sealed class SelectedEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet(
            "/selected",
            () => Results.Ok(new
            {
                Message = "This endpoint receives group and endpoint configuration."
            }));
}
