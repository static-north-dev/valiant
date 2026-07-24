using Microsoft.AspNetCore.Mvc;

namespace Valiant.Endpoints.AspNetCore.Samples.QueryBinding;

[ValiantEndpoint<QueryBindingEndpointGroup>]
[ValiantQueryParsing(
    SupportedArrayFormats =
    [
        QueryArrayFormat.Brackets,
        QueryArrayFormat.CommaSeparated,
        QueryArrayFormat.IndexedBrackets,
        QueryArrayFormat.RepeatedKeys
    ])]
public partial class ArrayQueryFormatsEndpoint : IEndpoint
{
    // repeated keys: /array-formats?ids=1&ids=2&ids=3
    // comma separated: /array-formats?ids=1,2,3
    // brackets: /array-formats?ids[]=1&ids[]=2&ids[]=3
    // indexed brackets: /array-formats?ids[2]=3&ids[0]=1&ids[1]=2
    // Every request binds Ids as [1, 2, 3].

    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder
            .MapGet("/array-formats", (Request request) => TypedResults.Ok(request));
    }

    public partial record Request
    {
        [FromQuery]
        public int[]? Ids { get; init; }
    }
}
