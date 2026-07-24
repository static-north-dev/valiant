---
title: Query parameters
description: Complete parsing and serialization formats, options, supported values, and structured errors.
---

# Query parameter parsing and serialization

Inbound parsing and outbound serialization use separate options. An API can accept several client conventions while emitting one stable format.

Generated endpoint binding is opt-in and disabled by default:

```csharp
builder.Services.AddValiantEndpoints(options =>
{
    options.QueryParameters.EnableBinding = true;
});
```

The direct literal assignment must be in the same compilation as the endpoints so the source generator can add the binding members. Direct calls to `QueryStringParser` and `QueryStringSerializer` do not require endpoint binding to be enabled.

`EnableBinding` is a compile-time opt-in, not a fully dynamic runtime switch. Whole-request `BindAsync` and `[AsParameters]` processing can resolve runtime options, but a generated direct `TryParse(string, out T)` has no `HttpContext`. Once a recognized literal `true` causes that parser to be compiled, another host or runtime override to `false` cannot remove or neutralize it. Compile the endpoint project with no recognized `true` assignment when `[ValiantQueryParsing]` must be ignored.

## Defaults

```csharp
var parsing = new QueryParsingOptions
{
    SupportedArrayFormats = [QueryArrayFormat.RepeatedKeys],
    SupportedObjectFormats = [QueryObjectFormat.Flat],
    EnableJsonQueryValues = false
};

var serialization = new QuerySerializationOptions
{
    ArrayFormat = QueryArrayFormat.RepeatedKeys,
    ObjectFormat = QueryObjectFormat.Flat
};
```

## Array formats

| Value | Example |
| --- | --- |
| `RepeatedKeys` | `?ids=1&ids=2&ids=3` |
| `CommaSeparated` | `?ids=1,2,3` |
| `Brackets` | `?ids[]=1&ids[]=2&ids[]=3` |
| `IndexedBrackets` | `?ids[0]=1&ids[1]=2&ids[2]=3` |

Enable every accepted inbound format explicitly:

```csharp
new QueryParsingOptions
{
    SupportedArrayFormats =
    [
        QueryArrayFormat.RepeatedKeys,
        QueryArrayFormat.CommaSeparated,
        QueryArrayFormat.Brackets,
        QueryArrayFormat.IndexedBrackets
    ]
};
```

Repeated and bracket values preserve query order; indexed brackets sort by numeric index. Empty items and disabled formats return structured errors.

## Object formats

| Value | Example |
| --- | --- |
| `Flat` | `?brand=tesla&year=2025` |
| `DotNotation` | `?filter.brand=tesla&filter.year=2025` |
| `DeepObject` | `?filter[brand]=tesla&filter[year]=2025` |

Duplicate scalar values use the last value. Flat output cannot preserve nested paths; choose dot notation, deep object, or explicitly enabled JSON for nested serialization.

## JSON query values

`QueryStringParser` and generated whole-request binding disable JSON by default. Opt in only for endpoints that intentionally accept URL-encoded JSON objects:

```csharp
new QueryParsingOptions { EnableJsonQueryValues = true };
```

For a single mapped endpoint using generated whole-request or `[AsParameters]` binding, replace the global parsing policy:

```csharp
return builder.MapGet("/search", Handle)
    .WithQueryParsingOptions(new QueryParsingOptions
    {
        SupportedObjectFormats = [QueryObjectFormat.Flat],
        EnableJsonQueryValues = true
    });
```

For `[AsParameters]`, the policy is applied after ASP.NET Core has performed native property binding. Formats that fail that initial conversion, such as comma-separated input for an `int[]` property, require generated whole-request binding instead.

For a partial source-generated override, place `[ValiantQueryParsing]` on the endpoint class:

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

Precedence is explicit attribute properties, then the last `.WithQueryParsingOptions(...)` call, then global `QueryParameters.Parsing`, then defaults. Omitted or `null` array properties inherit; `[]` accepts no formats in that category. `SupportedArrayFormats` and `SupportedObjectFormats` affect full-query binding only. `JsonValues` affects full-query binding when explicitly named and always controls generated direct complex `[FromQuery]` parsers.

For example, direct complex `[FromQuery]` parameters can disable their generated JSON parser with the same attribute:

```csharp
[ValiantEndpoint]
[ValiantQueryParsing(JsonValues = JsonQueryValues.Disabled)]
public sealed class SearchEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet("/search", static ([FromQuery] SearchRequest request) => Results.Ok(request));
}
```

When the endpoint project is compiled with `options.QueryParameters.EnableBinding = false`, the whole attribute is ignored and no Valiant query parser is generated. A later runtime-only override cannot change an already-generated direct parser. When binding is enabled, no attribute and `[ValiantQueryParsing]` both leave JSON enabled for the generated direct parser. Explicit `Disabled` returns HTTP 400 without a Valiant direct-query filter. Array and object format properties do not affect direct parameters. User-authored `TryParse` methods remain user-controlled, and conflicting policies for the same generated parser type report `VLE011`.

```http
?filter=%7B%22brand%22%3A%22tesla%22%2C%22year%22%3A2025%7D
```

## Direct parsing

```csharp
var result = QueryStringParser.Parse<SearchQuery>(
    "?ids=1,2,3&filter.brand=tesla",
    new QueryParsingOptions
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
    });

if (!result.IsSuccess)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"{error.Path} [{error.Code}] {error.Message}");
    }
}
```

Primitive arrays and `List<T>` support `string`, `int`, `long`, `Guid`, `bool`, `decimal`, and `DateTime`; nullable primitive scalars are supported. Nested object binding supports at least one level.

## Direct serialization

```csharp
var result = QueryStringSerializer.Serialize(
    new
    {
        ids = new[] { 1, 2, 3 },
        filter = new { brand = "tesla", year = 2025 }
    },
    new QuerySerializationOptions
    {
        ArrayFormat = QueryArrayFormat.CommaSeparated,
        ObjectFormat = QueryObjectFormat.DotNotation
    });

// ?ids=1,2,3&filter.brand=tesla&filter.year=2025
```

The serializer URL-encodes keys and values, preserves array order, and skips null values. It never negotiates an output format: one array and one object format are selected explicitly.

## Structured errors

`QueryErrorKind.Validation` means the supplied value or target shape is invalid—for example, an invalid `Guid`, empty array element, or unsupported type. `QueryErrorKind.Configuration` means the request used a disabled format or serialization options could not represent the requested shape.

```csharp
public sealed class QueryError
{
    public string Path { get; }
    public string Code { get; }
    public string Message { get; }
    public QueryErrorKind Kind { get; }
}
```
