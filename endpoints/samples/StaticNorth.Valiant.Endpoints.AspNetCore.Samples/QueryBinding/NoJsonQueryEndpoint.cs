using Microsoft.AspNetCore.Mvc;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.Samples.QueryBinding;

[ValiantEndpoint<QueryBindingEndpointGroup>]
[ValiantQueryParsing(JsonValues = JsonQueryValues.Disabled)]
public partial class NoJsonQueryEndpoint : IEndpoint
{
    // Returns 400 because this endpoint disables JSON query values:
    // /no-json?request=%7B%22name%22%3A%22John%22%2C%22age%22%3A30%2C%22address%22%3A%7B%22street%22%3A%22123%20Main%20St%22%2C%22city%22%3A%22Anytown%22%7D%7D

    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet("/no-json", ([FromQuery] Request request) => TypedResults.Ok(request));
    }

    public partial record Request
    {
        public string? Name { get; init; }

        public int Age { get; init; }

        public AddressDetails? Address { get; init; }

        public record AddressDetails
        {
            public string? Street { get; init; }

            public string? City { get; init; }
        }
    }
}
