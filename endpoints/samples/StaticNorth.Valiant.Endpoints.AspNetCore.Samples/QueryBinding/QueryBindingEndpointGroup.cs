namespace StaticNorth.Valiant.Endpoints.AspNetCore.Samples.QueryBinding;

[ValiantEndpointGroup]
public class QueryBindingEndpointGroup : IEndpointGroup
{
    public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder)
    {
        return builder
            .MapGroup("/");
    }
}
