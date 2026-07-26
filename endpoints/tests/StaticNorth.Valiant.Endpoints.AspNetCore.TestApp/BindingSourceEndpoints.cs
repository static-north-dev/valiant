using Microsoft.AspNetCore.Mvc;
using StaticNorth.Valiant.Endpoints.AspNetCore;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.TestApp;

public sealed record BindingSourcePayload(string Value);

public sealed class BindingSourceService
{
    public string Value => "service-value";
}

public sealed class CustomBindingSourceValue(string value)
{
    public string Value { get; } = value;

    public static ValueTask<CustomBindingSourceValue?> BindAsync(
        HttpContext context,
        System.Reflection.ParameterInfo parameter)
    {
        return ValueTask.FromResult<CustomBindingSourceValue?>(
            new CustomBindingSourceValue(
                context.Request.Headers["X-Custom-Binding-Value"].ToString()));
    }
}

public sealed record AsParametersBindingSourceRequest
{
    [FromQuery(Name = "id")]
    public int Id { get; init; }

    [FromQuery(Name = "name")]
    public string? Name { get; init; }
}

[ValiantEndpoint]
public sealed class FromQueryBindingSourceEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/integration/binding-sources/query",
            ([FromQuery(Name = "id")] int id) =>
                TypedResults.Ok(new BindingSourcePayload(id.ToString())));
    }
}

[ValiantEndpoint]
public sealed class AsParametersBindingSourceEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/integration/binding-sources/as-parameters",
            ([AsParameters] AsParametersBindingSourceRequest request) =>
                TypedResults.Ok(new BindingSourcePayload($"{request.Id}:{request.Name}")))
            .WithQueryParsingOptions(TestQueryParsingOptions.LocalAsParametersScalar());
    }
}

[ValiantEndpoint]
public sealed class FromRouteBindingSourceEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/integration/binding-sources/route/{id}",
            ([FromRoute(Name = "id")] int id) =>
                TypedResults.Ok(new BindingSourcePayload(id.ToString())));
    }
}

[ValiantEndpoint]
public sealed class FromHeaderBindingSourceEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/integration/binding-sources/header",
            ([FromHeader(Name = "X-Binding-Value")] string value) =>
                TypedResults.Ok(new BindingSourcePayload(value)));
    }
}

[ValiantEndpoint]
public sealed class FromBodyBindingSourceEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapPost(
            "/integration/binding-sources/body",
            ([FromBody] BindingSourcePayload payload) => TypedResults.Ok(payload));
    }
}

[ValiantEndpoint]
public sealed class FromFormBindingSourceEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder
            .MapPost(
                "/integration/binding-sources/form",
                ([FromForm(Name = "value")] string value) =>
                    TypedResults.Ok(new BindingSourcePayload(value)))
            .DisableAntiforgery();
    }
}

[ValiantEndpoint]
public sealed class FromServicesBindingSourceEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/integration/binding-sources/services",
            ([FromServices] BindingSourceService service) =>
                TypedResults.Ok(new BindingSourcePayload(service.Value)));
    }
}

[ValiantEndpoint]
public sealed class CustomBindingSourceEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/integration/binding-sources/custom",
            (CustomBindingSourceValue value) =>
                TypedResults.Ok(new BindingSourcePayload(value.Value)));
    }
}

[ValiantEndpointGroup]
public sealed class BindingSourceEndpointGroup : IEndpointGroup
{
    public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder)
    {
        return builder.MapGroup("/integration/binding-sources/group");
    }
}

[ValiantEndpoint<BindingSourceEndpointGroup>]
public sealed class BindingSourceGroupEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/ping",
            () => TypedResults.Ok(new BindingSourcePayload("group-value")));
    }
}

[ValiantEndpoint<BindingSourceEndpointGroup>]
public sealed class BindingSourceGroupSiblingEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/sibling",
            () => TypedResults.Ok(new BindingSourcePayload("sibling-value")));
    }
}

[ValiantEndpointGroup]
public sealed class AllowedBindingSourceEndpointGroup : IEndpointGroup
{
    public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder)
    {
        return builder.MapGroup("/integration/binding-sources/allowed-group");
    }
}

[ValiantEndpoint<AllowedBindingSourceEndpointGroup>]
public sealed class AllowedBindingSourceGroupEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet(
            "/ping",
            () => TypedResults.Ok(new BindingSourcePayload("allowed-group-value")));
    }
}
