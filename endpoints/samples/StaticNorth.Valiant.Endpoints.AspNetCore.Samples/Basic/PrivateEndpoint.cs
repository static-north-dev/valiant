namespace StaticNorth.Valiant.Endpoints.AspNetCore.Samples.Basic;

[ValiantEndpoint<PrivateEndpointGroup>]
public class PrivateEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet("/hello", () => "Hello, world private!");
    }
}