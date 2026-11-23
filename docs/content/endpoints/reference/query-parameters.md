---
title: Query parameters
description: Complete parsing and serialization formats, options, supported values, and structured errors.
---

# Query parameter parsing and serialization

Inbound parsing and outbound serialization use separate options. An API can accept several client conventions while emitting one stable format.

Opt into generated endpoint binding in the project that declares the endpoints.

```csharp
using StaticNorth.Valiant.Endpoints.AspNetCore;

[assembly: ValiantGenerateQueryBinding]
```

The assembly opt-in enables whole-request, `[AsParameters]`, and direct query binding. A direct literal `EnableBinding = true` assignment in the endpoint assembly remains supported. Standalone calls to `QueryStringParser` and `QueryStringSerializer` are available by default. See [request binding setup](../guides/request-binding.md).

Apps targeting .NET 11 or later default to strict parsing. Earlier targets keep 1.2.1 behavior, even when built with the .NET 11 SDK. Override `QueryParsingOptions.UseStrictBinding` when calling `QueryStringParser`, in `AddValiantEndpoints` via `options.QueryParameters.Parsing`, or in `WithQueryParsingOptions` on a route. Strict parsing rejects conflicting shapes and missing required members, supports combined object and array path formats, and uses `[FromQuery]` aliases for generated request objects.

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

Repeated and bracket values preserve query order. Indexed brackets sort by numeric index. Empty string items are valid in string collections. Empty items for other element types and disabled formats return structured errors.

## Object formats

| Value | Example |
| --- | --- |
| `Flat` | `?brand=tesla&year=2025` |
| `DotNotation` | `?filter.brand=tesla&filter.year=2025` |
| `DeepObject` | `?filter[brand]=tesla&filter[year]=2025` |

Duplicate scalar values use the last value. With `UseStrictBinding` enabled, a single property path cannot combine a direct value with nested keys. Strict parsing also supports combining dot notation and deep object with bracket arrays. For example, `filter.ids[0]=1` requires `DotNotation` and `IndexedBrackets`, while `filter[ids][]=1` requires `DeepObject` and `Brackets`.

Choose dot notation, deep object, or explicitly enabled JSON to preserve nested paths.

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

Precedence is explicit attribute properties, the last `.WithQueryParsingOptions(...)` call, conditional registration, global `QueryParameters.Parsing`, and defaults, in that order. Omitted or `null` array properties inherit. An empty array disables every format in that category. `SupportedArrayFormats` and `SupportedObjectFormats` affect full-query binding only. `JsonValues` affects full-query binding when explicitly named and always controls generated direct complex `[FromQuery]` parsers.

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

Generated direct parsers keep their compiled JSON policy regardless of the host's runtime setting. JSON is enabled when the policy attribute is absent or parameterless. Explicit `Disabled` returns HTTP 400 through ASP.NET Core. The array and object formats apply to full-query binding only. User-authored `TryParse` methods retain their own behavior. Conflicting policies for the same generated parser type report `VLE011`.

On .NET 11 and later targets, generated direct `TryParse` methods return `false` for malformed or disabled JSON. Earlier targets retain the 1.2.1 `BadHttpRequestException` behavior for direct CLR callers. Set `UseStrictBinding` on the `ValiantGenerateQueryBinding` assembly attribute to override the default in the assembly that declares the binding. HTTP requests receive 400 in both modes.

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

Use an object with named properties as the root value. Arrays and `List<T>` properties support `string`, `int`, `long`, `Guid`, `bool`, `decimal`, `DateTime`, and enum values. Nullable primitive scalars are supported. Nested object binding reports an error when it reaches the 64-level depth limit.

Valiant's field-by-field object binding uses properties with public setters, including public `init` accessors. Standalone parsing and 1.2.1-compatible whole-request binding use CLR property names. Strict whole-request binding uses root properties marked with `[FromQuery]` and honors any `Name` alias. Strict parsing reports missing `required` properties using their full paths unless a constructor marked with `[SetsRequiredMembers]` supplies defaults.

Repeated and delimited values require a one-dimensional array or a type assignable from `List<T>`. An incompatible collection type returns `UnsupportedQueryType`. With JSON query values enabled, the parser can also bind collection types that System.Text.Json can deserialize. Unsupported JSON target types return `UnsupportedQueryType`.

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

The serializer reads public getters, URL-encodes keys and values, and preserves array order. Delimited formats skip null items and omit empty collections. When an optional key is absent, parsing keeps the target property's initialized or default value. To round-trip an omitted empty collection as empty, initialize that property to an empty collection. Complex collection shapes require JSON query values. Strict parsing with JSON disabled treats a scalar value for a collection of strings as literal text. Dot-notation and deep-object serialization return a structured error after 64 nested levels.

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
