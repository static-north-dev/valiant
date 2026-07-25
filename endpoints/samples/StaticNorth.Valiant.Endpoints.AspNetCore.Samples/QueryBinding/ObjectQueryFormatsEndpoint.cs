using Microsoft.AspNetCore.Mvc;

namespace Valiant.Endpoints.AspNetCore.Samples.QueryBinding;

[ValiantEndpoint<QueryBindingEndpointGroup>]
[ValiantQueryParsing(
    SupportedObjectFormats =
    [
       QueryObjectFormat.Flat,
       QueryObjectFormat.DeepObject,
       QueryObjectFormat.DotNotation
    ])]
public partial class ObjectQueryFormatsEndpoint : IEndpoint
{
    // flat: /object-formats?id=1&name=tesla
    // dot notation: /object-formats?filter.id=1&filter.name=tesla
    // deep object: /object-formats?filter[id]=1&filter[name]=tesla
    // Flat values bind to Request; nested formats bind to Request.Filter.

    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder
            .MapGet("/object-formats", (Request request) => TypedResults.Ok(request));
    }

    public partial record Request
    {
        [FromQuery]
        public int? Id { get; init; }

        [FromQuery]
        public string? Name { get; init; }

        [FromQuery]
        public Filter? Filter { get; init; }
    }

    public partial record Filter
    {
        public int? Id { get; init; }

        public string? Name { get; init; }
    }
}
