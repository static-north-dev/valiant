using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.TestApp;

internal static class TestQueryParsingOptions
{
    internal static QueryParsingOptions LocalObject()
    {
        return new QueryParsingOptions
        {
            SupportedArrayFormats = [QueryArrayFormat.RepeatedKeys],
            SupportedObjectFormats = [QueryObjectFormat.Flat],
            EnableJsonQueryValues = true
        };
    }

    internal static QueryParsingOptions LocalArray()
    {
        return new QueryParsingOptions
        {
            SupportedArrayFormats = [QueryArrayFormat.CommaSeparated],
            SupportedObjectFormats = [QueryObjectFormat.Flat],
            EnableJsonQueryValues = false
        };
    }

    internal static QueryParsingOptions LocalAsParametersArray()
    {
        return new QueryParsingOptions
        {
            SupportedArrayFormats = [QueryArrayFormat.IndexedBrackets],
            SupportedObjectFormats = [QueryObjectFormat.Flat],
            EnableJsonQueryValues = false
        };
    }

    internal static QueryParsingOptions LocalAsParametersScalar()
    {
        return new QueryParsingOptions
        {
            SupportedArrayFormats = [QueryArrayFormat.RepeatedKeys],
            SupportedObjectFormats = [QueryObjectFormat.Flat],
            EnableJsonQueryValues = false
        };
    }
}

public sealed partial record GeneratedObjectRequest
{
    [FromQuery]
    public int? Id { get; init; }

    [FromQuery]
    public QueryFilter? Filter { get; init; }

    [FromQuery(Name = "page-size")]
    public int? Limit { get; init; }

    public string Label { get; init; } = "default";
}

public sealed record AsParametersObjectRequest
{
    [FromQuery]
    public int? Id { get; init; }

    [FromQuery]
    public QueryFilter? Filter { get; init; }
}

public sealed partial record QueryFilter
{
    public int? Id { get; init; }
}

public class PagingQuery
{
    [FromQuery(Name = "page.size")]
    public int? Limit { get; init; }
}

public sealed partial class InheritedPagingQuery : PagingQuery
{
    [FromQuery(Name = "term")]
    public required string Search { get; init; }
}

[ValiantEndpoint]
public sealed class InheritedPagingEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet("/integration/query/inherited", (InheritedPagingQuery query) => TypedResults.Ok(query))
            .WithQueryParsingOptions(new QueryParsingOptions());
}

public sealed class FilterObservationRequest
{
    [FromQuery]
    public QueryFilter? Filter { get; init; }

    [FromQuery]
    public DateOnly? Day { get; init; }
}

[ValiantEndpointGroup]
public sealed class FilterObservationGroup : IEndpointGroup
{
    public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) =>
        builder.MapGroup("/integration/filter-observation").AddEndpointFilter(static (context, next) =>
        {
            context.HttpContext.Response.Headers["X-Group-Observed-Filter"] =
                context.GetArgument<FilterObservationRequest>(0).Filter?.Id?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
            return next(context);
        });
}

[ValiantEndpoint<FilterObservationGroup>]
public sealed class FilterObservationEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet("/observe", ([AsParameters] FilterObservationRequest request) => TypedResults.Ok(request))
            .AddEndpointFilter(static (context, next) =>
            {
                context.HttpContext.Response.Headers["X-Route-Observed-Filter"] =
                    context.GetArgument<FilterObservationRequest>(0).Filter?.Id?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
                return next(context);
            })
            .WithQueryParsingOptions(new QueryParsingOptions
            {
                SupportedObjectFormats = [QueryObjectFormat.Flat, QueryObjectFormat.DotNotation]
            });
}

public sealed partial record GeneratedArrayRequest
{
    [FromQuery]
    public int[]? Ids { get; init; }
}

public sealed record AsParametersArrayRequest
{
    [FromQuery]
    public int[]? Ids { get; init; }
}

public sealed partial record DirectJsonEnabledRequest
{
    public int? Id { get; init; }
}

public sealed partial record DirectJsonDisabledRequest
{
    public int? Id { get; init; }
}

public sealed partial record DirectJsonDefaultRequest
{
    public int? Id { get; init; }
}

[ValiantEndpoint]
public sealed class GeneratedGlobalObjectEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/integration/generated/object/global",
            (GeneratedObjectRequest request) => TypedResults.Ok(request));
    }
}

[ValiantEndpoint]
public sealed class GeneratedLocalObjectEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder
            .MapGet(
                "/integration/generated/object/local",
                (GeneratedObjectRequest request) => TypedResults.Ok(request))
            .WithQueryParsingOptions(TestQueryParsingOptions.LocalObject());
    }
}

[ValiantEndpoint]
[ValiantQueryParsing(
    SupportedObjectFormats = [QueryObjectFormat.DeepObject],
    JsonValues = JsonQueryValues.Disabled)]
public sealed class GeneratedAttributeObjectEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder
            .MapGet(
                "/integration/generated/object/attribute",
                (GeneratedObjectRequest request) => TypedResults.Ok(request))
            .WithQueryParsingOptions(TestQueryParsingOptions.LocalObject());
    }
}

[ValiantEndpoint]
public sealed class AsParametersGlobalObjectEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/integration/as-parameters/object/global",
            Handle);
    }

    private static Ok<AsParametersObjectRequest> Handle([AsParameters] AsParametersObjectRequest request)
        => TypedResults.Ok(request);
}

[ValiantEndpoint]
public sealed class QueryBindingFailureEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        => builder.MapGet("/integration/as-parameters/binding-failure/{id}",
            static (int id, [AsParameters] AsParametersObjectRequest request) => TypedResults.Ok(id));
}

[ValiantEndpoint]
public sealed class AsParametersLocalObjectEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder
            .MapGet(
                "/integration/as-parameters/object/local",
                ([AsParameters] AsParametersObjectRequest request) => TypedResults.Ok(request))
            .WithQueryParsingOptions(TestQueryParsingOptions.LocalObject());
    }
}

[ValiantEndpoint]
[ValiantQueryParsing(
    SupportedObjectFormats = [QueryObjectFormat.DeepObject],
    JsonValues = JsonQueryValues.Disabled)]
public sealed class AsParametersAttributeObjectEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder
            .MapGet(
                "/integration/as-parameters/object/attribute",
                ([AsParameters] AsParametersObjectRequest request) => TypedResults.Ok(request))
            .WithQueryParsingOptions(TestQueryParsingOptions.LocalObject());
    }
}

[ValiantEndpoint]
[ValiantQueryParsing(SupportedObjectFormats = [QueryObjectFormat.Flat])]
public sealed class GeneratedGlobalArrayEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/integration/generated/array/global",
            (GeneratedArrayRequest request) => TypedResults.Ok(request));
    }
}

[ValiantEndpoint]
public sealed class GeneratedLocalArrayEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder
            .MapGet(
                "/integration/generated/array/local",
                (GeneratedArrayRequest request) => TypedResults.Ok(request))
            .WithQueryParsingOptions(TestQueryParsingOptions.LocalArray());
    }
}

[ValiantEndpoint]
[ValiantQueryParsing(
    SupportedArrayFormats = [QueryArrayFormat.Brackets],
    SupportedObjectFormats = [QueryObjectFormat.Flat])]
public sealed class GeneratedAttributeArrayEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder
            .MapGet(
                "/integration/generated/array/attribute",
                (GeneratedArrayRequest request) => TypedResults.Ok(request))
            .WithQueryParsingOptions(TestQueryParsingOptions.LocalArray());
    }
}

[ValiantEndpoint]
[ValiantQueryParsing(SupportedObjectFormats = [QueryObjectFormat.Flat])]
public sealed class AsParametersGlobalArrayEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/integration/as-parameters/array/global",
            ([AsParameters] AsParametersArrayRequest request) => TypedResults.Ok(request));
    }
}

[ValiantEndpoint]
public sealed class AsParametersLocalArrayEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder
            .MapGet(
                "/integration/as-parameters/array/local",
                ([AsParameters] AsParametersArrayRequest request) => TypedResults.Ok(request))
            .WithQueryParsingOptions(TestQueryParsingOptions.LocalAsParametersArray());
    }
}

[ValiantEndpoint]
[ValiantQueryParsing(
    SupportedArrayFormats = [QueryArrayFormat.Brackets],
    SupportedObjectFormats = [QueryObjectFormat.Flat])]
public sealed class AsParametersAttributeArrayEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder
            .MapGet(
                "/integration/as-parameters/array/attribute",
                ([AsParameters] AsParametersArrayRequest request) => TypedResults.Ok(request))
            .WithQueryParsingOptions(TestQueryParsingOptions.LocalAsParametersArray());
    }
}

[ValiantEndpoint]
[ValiantQueryParsing(JsonValues = JsonQueryValues.Enabled)]
public sealed class DirectJsonEnabledEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/integration/direct/enabled",
            Handle);
    }

    private static Ok<DirectJsonEnabledRequest> Handle([FromQuery(Name = "payload")] DirectJsonEnabledRequest request)
        => TypedResults.Ok(request);
}

[ValiantEndpoint]
[ValiantQueryParsing(JsonValues = JsonQueryValues.Disabled)]
public sealed class DirectJsonDisabledEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder
            .MapGet(
                "/integration/direct/disabled",
                ([FromQuery(Name = "payload")] DirectJsonDisabledRequest request) => TypedResults.Ok(request))
            .WithQueryParsingOptions(TestQueryParsingOptions.LocalObject());
    }
}

[ValiantEndpoint]
public sealed class DirectJsonDefaultEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/integration/direct/default",
            ([FromQuery(Name = "payload")] DirectJsonDefaultRequest request) => TypedResults.Ok(request));
    }
}

[ValiantEndpoint]
public sealed class DirectJsonOptionalEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/integration/direct/optional",
            ([FromQuery(Name = "payload")] DirectJsonDefaultRequest? request) =>
                TypedResults.Ok(request?.Id ?? -1));
    }
}
