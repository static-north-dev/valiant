namespace Valiant.Endpoints.AspNetCore.Samples.Basic;

[ValiantEndpoint]
public class PublicEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet("/hello", () => "Hello, world!");
    }
}