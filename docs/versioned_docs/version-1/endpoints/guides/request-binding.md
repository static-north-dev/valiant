---
title: Request binding
description: Bind complex query objects through generated requests, AsParameters, and FromQuery compatibility shims.
---

# Request binding

Valiant query binding is disabled by default, leaving request parameters to native ASP.NET Core binding. Enable generated binding in the same project as the endpoint declarations with a direct assignment:

```csharp
builder.Services.AddValiantEndpoints(options =>
{
    options.QueryParameters.EnableBinding = true;
});
```

Changing the literal between `true` and `false` adds or removes generated query binding during the normal rebuild. Helper methods, runtime variables, and a host configuration in another assembly are not compile-time opt-ins.

:::caution Compile-time boundary

`EnableBinding` is not a fully dynamic runtime switch. Whole-request `BindAsync` and `[AsParameters]` processing can read runtime options, but a generated direct `TryParse(string, out T)` has no `HttpContext`. If the endpoint compilation contains a recognized literal `true`, another host or later runtime override to `false` cannot remove or neutralize that direct parser. Compile the endpoint project with no recognized `true` assignment when `[ValiantQueryParsing]` must be ignored.

:::

When enabled, Valiant connects its configurable query parser to generated endpoints in three binding shapes.

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

public sealed record CarFilter(int? Year, string? Brand);
```

Valiant emits `BindAsync(HttpContext, ParameterInfo)` on `ListCarsRequest` and parses the full query through the configured policy.
If the request is nested inside an endpoint or another type, every containing declaration must also be `partial` so generated code can reopen the complete declaration chain.

## `[AsParameters]`

ASP.NET Core first constructs the request; a Valiant endpoint filter then reparses the full query and copies `[FromQuery]` values back:

```csharp
return builder.MapGet("/cars", static ([AsParameters] ListCarsRequest request) =>
    Results.Ok(request.Filter));

public sealed record ListCarsRequest
{
    [FromQuery]
    public CarFilter? Filter { get; init; }
}

public sealed partial record CarFilter(int? Year, string? Brand);
```

The complex `[FromQuery]` type must be `partial` in this shape. Valiant generates a `TryParse(string, out T)` shim because ASP.NET Core checks property bindability before endpoint filters run.
The same containing-type rule applies when that complex type is nested.

The Valiant filter runs after native property binding, so it cannot recover from a format that makes ASP.NET Core fail the initial property conversion. For example, comma-separated `?ids=1,2,3` fails before the filter for an `int[] Ids` property. Use generated whole-request binding when every configured format must work without native `[AsParameters]` constraints.

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

The policy is compiled into the generated method, so direct binding does not use an endpoint filter. When the endpoint project is compiled with `options.QueryParameters.EnableBinding = false`, the attribute is ignored and no Valiant query parser is emitted. A later runtime-only override cannot change an already-generated direct parser; see the compile-time boundary above. When binding is enabled, JSON remains enabled when the attribute is absent or parameterless. Explicit `Disabled` returns HTTP 400. `SupportedArrayFormats` and `SupportedObjectFormats` on the attribute do not affect direct parameters. The attribute does not change a user-authored `TryParse`. Conflicting policies for the same generated parser type report `VLE011`.

## Failure response

Generated binders and the Valiant query-binding filter return HTTP 400 with structured `QueryError` entries when values cannot be converted or a client uses a disabled format. Each entry identifies a `Path`, stable `Code`, human-readable `Message`, and `Kind` (`Validation` or `Configuration`).

## Configure accepted formats

```csharp
builder.Services.AddValiantEndpoints(options =>
{
    options.QueryParameters.EnableBinding = true;
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

This is a full replacement, not a merge. Other endpoints continue using the global parsing options.

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

Explicit attribute properties win over `.WithQueryParsingOptions(...)`, which wins over global parsing options and then defaults. Omitted or `null` array properties inherit; `[]` disables every format in that category. The array and object properties affect full-query binding only. Explicit `JsonValues` affects full-query binding and generated direct complex `[FromQuery]`; when omitted, full-query binding inherits while direct JSON remains enabled.

See the [query parameter reference](../reference/query-parameters.md) for all formats and direct parser/serializer APIs.
