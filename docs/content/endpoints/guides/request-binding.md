---
title: Request binding
description: Bind complex query objects through generated requests, AsParameters, and FromQuery compatibility shims.
---

# Request binding

Valiant query binding is disabled by default, leaving request parameters to native ASP.NET Core binding. Add this assembly attribute in the project that declares the endpoints.

```csharp
using StaticNorth.Valiant.Endpoints.AspNetCore;

[assembly: ValiantGenerateQueryBinding(UseStrictBinding = true)]
```

Register the endpoint assembly with `AddValiantEndpoints`. The host can configure the parsing formats for whole-request and `[AsParameters]` binding.

```csharp
builder.Services.AddValiantEndpoints(options =>
{
    options.QueryParameters.Parsing = new QueryParsingOptions
    {
        UseStrictBinding = true,
        SupportedObjectFormats = [QueryObjectFormat.Flat, QueryObjectFormat.DotNotation]
    };
});
```

Existing projects can still opt into generation with a direct literal `true` assignment in the `AddValiantEndpoints` setup lambda in the endpoint assembly. The assembly attribute also works when registration lives in a helper or a different assembly.

These examples select strict binding on every target. With both settings omitted, .NET 11 and later targets default to strict binding, and earlier targets use 1.2.1 behavior. The runtime setting controls full-query parsing, alias binding, required members, and structured whole-request errors. The assembly setting selects strict generated `TryParse` methods.

:::caution Compile-time boundary

The endpoint assembly's opt-in enables all three generated binding forms. Changing `EnableBinding` in the host leaves generated binding enabled. To disable it, remove the assembly attribute and any recognized literal `true` assignment, then rebuild. Whole-request and `[AsParameters]` binding read runtime parsing options. Direct `TryParse` uses the JSON policy compiled into the generated parser.

:::

Choose a binding shape based on what the client sends. Use whole-request binding for complex queries that need consistent format handling and structured errors. The setup above enables dot notation for nested values in addition to the default flat format. Handlers can be inline or passed as named method groups.

| Handler parameter | Example request | Failure response |
| --- | --- | --- |
| Whole request with generated `BindAsync` | `/cars?filter.year=2025&filter.brand=Volvo` | Mapped Valiant endpoints return structured query errors using the global or per-endpoint parsing policy. |
| `[AsParameters]` with complex query properties | `/cars?filter.year=2025&filter.brand=Volvo` | ASP.NET Core handles native binding failures. After native binding succeeds, Valiant can return HTTP 400 with structured `QueryError` entries. |
| Direct complex `[FromQuery] CarFilter filter` | `/cars?filter=%7B%22year%22%3A2025%7D` | ASP.NET Core handles HTTP 400 when generated `TryParse` rejects the JSON. |

The first two examples produce a filter with `Year = 2025` and `Brand = "Volvo"`. In the direct example, the URL-encoded value is `{"year":2025}`.

## Whole request binding

Make the request `partial` and place `[FromQuery]` on query members:

```csharp
return builder.MapGet("/cars", static (ListCarsRequest request) =>
    Results.Ok(request.Filter));

public sealed partial record ListCarsRequest
{
    [FromQuery]
    public CarFilter? Filter { get; init; }
}

public sealed record CarFilter
{
    public int? Year { get; init; }
    public string? Brand { get; init; }
}
```

Valiant emits `BindAsync(HttpContext, ParameterInfo)` on `ListCarsRequest`. Only root properties marked `[FromQuery]` with public setters participate, including public `init` accessors. Set `[FromQuery(Name = "filter")]` to use a query key different from the CLR property name. Nested filter members use their public property names.

Omitting a `required` query property on the request or on a supplied nested query object returns a `MissingQueryValue` error with its full path. A constructor marked with `[SetsRequiredMembers]` can supply defaults.
If the request is nested inside an endpoint or another type, every containing declaration must also be `partial` so generated code can reopen the complete declaration chain.

## `[AsParameters]`

ASP.NET Core first constructs the request. The Valiant binder applies supplied `[FromQuery]` values using the configured formats and aliases before other endpoint filters run. Properties using their own native parser keep the value ASP.NET Core bound. Properties with no matching query key keep their existing values.

```csharp
return builder.MapGet("/cars", static ([AsParameters] ListCarsRequest request) =>
    Results.Ok(request.Filter));

public sealed record ListCarsRequest
{
    [FromQuery]
    public CarFilter? Filter { get; init; }
}

public sealed partial record CarFilter
{
    public int? Year { get; init; }
    public string? Brand { get; init; }
}
```

The complex `[FromQuery]` type must be `partial` in this shape. Valiant generates a `TryParse(string, out T)` method that reads JSON for native ASP.NET Core property binding. Valiant applies the endpoint's configured parsing policy when it binds the full query.
The same containing-type rule applies when that complex type is nested.

The Valiant filter reparses the query only after native parameter binding succeeds. For example, ASP.NET Core rejects comma-separated `?ids=1,2,3` for an `int[] Ids` property and controls the resulting HTTP response. Use generated whole-request binding when every configured format must work without native `[AsParameters]` constraints.

## Direct complex `[FromQuery]`

Complex query types used directly also need to be source-declared and `partial` so the generator can add the compatibility parser.

```csharp
return builder.MapGet("/cars", static ([FromQuery] CarFilter filter) =>
    Results.Ok(filter));

public sealed partial record CarFilter(int? Year, string? Brand);
```

ASP.NET Core calls the generated `TryParse` method to bind the value. Configure its JSON policy on the endpoint:

```csharp
[ValiantEndpoint]
[ValiantQueryParsing(JsonValues = JsonQueryValues.Disabled)]
public sealed class CarsEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet("/cars", static ([FromQuery] CarFilter filter) => Results.Ok(filter));
}
```

The policy is compiled into the generated method. JSON is enabled when the policy attribute is absent or parameterless. In strict mode, generated `TryParse` returns `false` for malformed or disabled JSON. In 1.2.1 compatibility mode, it throws `BadHttpRequestException`. ASP.NET Core returns HTTP 400 in both modes. The array and object formats apply to full-query binding only. User-authored `TryParse` methods keep their own behavior. Conflicting policies for the same generated parser type report `VLE011`.

## Failure response

For example, `/cars?filter.year=abc` produces HTTP 400 in both full-query examples. With strict parsing, both the whole-request binder and the Valiant `[AsParameters]` filter return a JSON array with an `InvalidQueryValue` error for `Filter.Year` when Valiant handles the binding failure. In 1.2.1 compatibility mode, ASP.NET Core handles whole-request binding errors. Each structured entry identifies a `Path`, stable `Code`, human-readable `Message`, and `Kind` (`Validation` or `Configuration`).

## Configure accepted formats

```csharp
builder.Services.AddValiantEndpoints(options =>
{
    options.QueryParameters.Parsing = new QueryParsingOptions
    {
        SupportedArrayFormats =
        [
            QueryArrayFormat.RepeatedKeys,
            QueryArrayFormat.CommaSeparated
        ],
        SupportedObjectFormats =
        [
            QueryObjectFormat.Flat,
            QueryObjectFormat.DotNotation
        ]
    };
});
```

Replace the global parsing rules for whole-request and `[AsParameters]` binding on one endpoint when needed:

```csharp
return builder.MapGet("/search", Handle)
    .WithQueryParsingOptions(new QueryParsingOptions
    {
        SupportedObjectFormats = [QueryObjectFormat.Flat],
        EnableJsonQueryValues = true
    });
```

This replaces the base parsing options with a snapshot taken when the convention is added. Explicit source-generated overrides still take precedence.

When only selected settings differ, use a partial attribute override:

```csharp
[ValiantEndpoint]
[ValiantQueryParsing(
    SupportedArrayFormats =
    [
        QueryArrayFormat.RepeatedKeys,
        QueryArrayFormat.CommaSeparated
    ],
    SupportedObjectFormats =
    [
        QueryObjectFormat.Flat,
        QueryObjectFormat.DotNotation
    ],
    JsonValues = JsonQueryValues.Disabled)]
public sealed partial class SearchEndpoint : IEndpoint
{
    // ...
}
```

Explicit attribute properties win over `.WithQueryParsingOptions(...)`, conditional registration, global parsing options, and then defaults, in that order. Omitted or `null` array properties inherit. An empty array disables every format in that category. The array and object formats affect full-query binding only. Explicit `JsonValues` affects full-query binding and generated direct complex `[FromQuery]`. When omitted, full-query binding inherits and direct JSON remains enabled.

See the [query parameter reference](../reference/query-parameters.md) for all formats and direct parser/serializer APIs.
