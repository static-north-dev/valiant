# Query Parameter Parsing and Serialization

This document defines the configurable query parameter behavior in `Valiant.Endpoints.AspNetCore`.

The query parser and serializer intentionally have separate option types:

- `QueryParameterOptions` groups endpoint-level query parameter policy.
- `QueryParsingOptions` controls what inbound query formats are accepted.
- `QuerySerializationOptions` controls what outbound query formats are emitted.

Separating the policies lets an API accept several client conventions while still emitting one stable canonical format.

## Public API

```csharp
public sealed class QueryParameterOptions
{
    public bool EnableBinding { get; set; } = false;

    public QueryParsingOptions Parsing { get; set; } = new();

    public QuerySerializationOptions Serialization { get; set; } = new();
}
```

```csharp
public sealed class QueryParsingOptions
{
    public QueryArrayFormat[] SupportedArrayFormats { get; init; } =
    [
        QueryArrayFormat.RepeatedKeys
    ];

    public QueryObjectFormat[] SupportedObjectFormats { get; init; } =
    [
        QueryObjectFormat.Flat
    ];

    public bool EnableJsonQueryValues { get; init; } = false;
}
```

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ValiantQueryParsingAttribute : Attribute
{
    public QueryArrayFormat[]? SupportedArrayFormats { get; set; }

    public QueryObjectFormat[]? SupportedObjectFormats { get; set; }

    public JsonQueryValues JsonValues { get; set; } = JsonQueryValues.Enabled;
}
```

```csharp
public sealed class QuerySerializationOptions
{
    public QueryArrayFormat ArrayFormat { get; init; } =
        QueryArrayFormat.RepeatedKeys;

    public QueryObjectFormat ObjectFormat { get; init; } =
        QueryObjectFormat.Flat;

    public bool EnableJsonQueryValues { get; init; } = false;
}
```

```csharp
public enum QueryArrayFormat
{
    RepeatedKeys,
    CommaSeparated,
    Brackets,
    IndexedBrackets
}

public enum QueryObjectFormat
{
    Flat,
    DotNotation,
    DeepObject
}
```

## Default Behavior

Valiant query binding is disabled by default. Without an explicit opt-in, no query `BindAsync` or compatibility `TryParse` members are generated and `MapEndpoint` does not attach Valiant's query-binding filter, leaving binding to ASP.NET Core.

Enable it in the same project as the endpoint declarations:

```csharp
builder.Services.AddValiantEndpoints(options =>
{
    options.QueryParameters.EnableBinding = true;
});
```

The generator recognizes a direct compile-time `true` assignment in the setup lambda. A helper method, runtime variable, or configuration in a different assembly is not a source-generation opt-in. If any recognized assignment is `true`, query binding source is generated for that compilation.

This is a compile-time opt-in, not a fully dynamic runtime switch. Whole-request `BindAsync` and `[AsParameters]` processing can resolve runtime options, but a generated direct `TryParse(string, out T)` has no `HttpContext` and cannot observe a later runtime override. If one host configuration in the endpoint compilation contains a recognized `true`, running that compiled assembly from another host with `EnableBinding = false` does not remove or neutralize the generated direct parser. To ignore `[ValiantQueryParsing]`, compile the endpoint project with no recognized `true` assignment.

Default parsing accepts only:

```http
?ids=1&ids=2&ids=3
?brand=tesla&year=2025
```

Default parsing rejects:

```http
?ids=1,2,3
?ids[]=1&ids[]=2
?ids[0]=1&ids[1]=2
?filter.brand=tesla
?filter[brand]=tesla
?filter=%7B%22brand%22%3A%22tesla%22%7D
```

Default serialization emits repeated array keys and flat top-level objects.

## Parser Configuration

The parser only accepts formats explicitly listed in `SupportedArrayFormats` and `SupportedObjectFormats`.

```csharp
var options = new QueryParsingOptions
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
    ],
    EnableJsonQueryValues = false
};
```

With this configuration, the parser accepts:

```http
?ids=1&ids=2&ids=3
?ids=1,2,3
?filter.brand=tesla&filter.year=2025
```

It rejects:

```http
?ids[]=1&ids[]=2
?ids[0]=1&ids[1]=2
?filter[brand]=tesla&filter[year]=2025
?filter=%7B%22brand%22%3A%22tesla%22%7D
```

Rejected disabled formats return `QueryErrorKind.Configuration`.

## Endpoint Binding

When `EnableBinding` is `true`, Valiant supports full-query binding for endpoint request objects.

Without `[AsParameters]`, make the request type partial:

```csharp
return builder.MapGet("/cars", static (ListCarsRequest request) => Results.Ok());

public sealed partial record ListCarsRequest
{
    [FromQuery]
    public CarFilter? Filter { get; init; }
}

public sealed record CarFilter
{
    public int? Id { get; init; }
}
```

If the request is nested inside an endpoint or another type, the request and every containing declaration must be `partial`. The generator must reopen the complete declaration chain to emit `BindAsync` or `TryParse`.

When a Valiant endpoint handler has an unannotated partial request parameter with public `[FromQuery]` properties, the source generator emits:

```csharp
public static ValueTask<ListCarsRequest> BindAsync(HttpContext context, ParameterInfo parameter)
```

The generated binder first uses endpoint-local query parsing metadata, then `ValiantEndpointOptions.QueryParameters.Parsing` from DI, then parser defaults. It calls `QueryStringParser.Parse<T>` with the full request query string. This supports configured formats such as:

```http
?filter.id=1
?filter[id]=1
```

With `[AsParameters]`, Valiant attaches an endpoint filter after the endpoint is mapped:

```csharp
return builder.MapGet("/cars", static ([AsParameters] ListCarsRequest request) => Results.Ok());

public sealed record ListCarsRequest
{
    [FromQuery]
    public CarFilter? Filter { get; init; }
}

public sealed partial record CarFilter
{
    public int? Id { get; init; }
}
```

ASP.NET first creates the `[AsParameters]` request object through its normal property binding. The Valiant endpoint filter then reparses the full query string with `ValiantEndpointOptions.QueryParameters.Parsing` and copies configured `[FromQuery]` property values onto the request object before the handler runs.

Complex `[FromQuery]` property types in `[AsParameters]` request objects still need the generated `TryParse(string, out T)` compatibility method because ASP.NET validates property bindability before endpoint filters run. That is why `CarFilter` is partial in the `[AsParameters]` shape.

The filter runs after native property binding and cannot repair input that makes ASP.NET Core fail the initial property conversion. For example, `?ids=1,2,3` fails before the filter for an `int[] Ids` property. Prefer generated whole-request `BindAsync` binding when every configured format must work without native `[AsParameters]` constraints.

### Direct complex `[FromQuery]`

A complex type used directly with `[FromQuery]` must also be source-declared and `partial`:

```csharp
return builder.MapGet("/cars", static ([FromQuery] CarFilter filter) => Results.Ok(filter));

public sealed partial record CarFilter(int? Year, string? Brand);
```

ASP.NET Core binds the value through Valiant's generated `TryParse(string, out T)` method. Configure JSON support on the endpoint declaration:

```csharp
[ValiantEndpoint]
[ValiantQueryParsing(JsonValues = JsonQueryValues.Disabled)]
public sealed class CarsEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet("/cars", static ([FromQuery] CarFilter filter) => Results.Ok(filter));
}
```

The generator compiles this policy directly into `TryParse`; no endpoint filter is used for direct parameters. When the endpoint project is compiled with `options.QueryParameters.EnableBinding = false`, the attribute is ignored and no Valiant query parser is generated. A later runtime-only override cannot change an already-generated direct parser; see the compile-time opt-in note above. When binding is enabled, JSON remains enabled when the attribute is absent or parameterless. Explicit `Disabled` returns HTTP 400. User-authored `TryParse` methods are not overridden. Reusing one generated parser type with conflicting endpoint policies reports `VLE011`.

### Endpoint-local policy

Use `WithQueryParsingOptions` to replace the global parsing policy for generated whole-request and `[AsParameters]` binding on one endpoint:

```csharp
return builder.MapGet("/search", Handle)
    .WithQueryParsingOptions(new QueryParsingOptions
    {
        SupportedObjectFormats = [QueryObjectFormat.Flat],
        EnableJsonQueryValues = true
    });
```

Endpoint options replace rather than merge with the global options. The last call wins, sibling endpoints remain unchanged, and the metadata has no effect while Valiant query binding is disabled.

Use `[ValiantQueryParsing]` for a source-generated partial override on the endpoint declaration:

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

The effective policy is resolved in this order:

1. Explicit properties on `[ValiantQueryParsing]`.
2. The last `.WithQueryParsingOptions(...)` value.
3. `ValiantEndpointOptions.QueryParameters.Parsing`.
4. A new `QueryParsingOptions` instance with default values.

Omitting an array property or assigning `null` inherits it from the next level. Assigning `[]` explicitly accepts no formats in that category. Array order and values are preserved, including invalid enum values; the parser reports invalid values as `InvalidQueryParsingOptions` configuration errors.

`SupportedArrayFormats` and `SupportedObjectFormats` apply to full-query `BindAsync` and `[AsParameters]` binding only. `JsonValues` also controls direct complex `[FromQuery]`. For full-query binding, an omitted `JsonValues` property inherits; for direct binding, omission keeps JSON enabled for compatibility.

The whole attribute is inactive when query binding is disabled at source-generation time. A later runtime-only `EnableBinding = false` cannot remove a direct parser already compiled into an endpoint assembly.

## Array Parsing

| Format | Example | Ordering |
| --- | --- | --- |
| `RepeatedKeys` | `?ids=1&ids=2&ids=3` | Original query order |
| `CommaSeparated` | `?ids=1,2,3` | Split order after URL decoding |
| `Brackets` | `?ids[]=1&ids[]=2&ids[]=3` | Original query order |
| `IndexedBrackets` | `?ids[0]=1&ids[1]=2&ids[2]=3` | Numeric index order |

Rules:

- Array values bind to arrays and `List<T>`.
- Supported element types are `string`, `int`, `long`, `Guid`, `bool`, `decimal`, and `DateTime`.
- Repeated keys preserve the query string order.
- Indexed brackets sort by numeric index.
- Comma-separated values are split after URL decoding.
- Empty array elements, such as `?ids=1,,3`, produce validation errors.
- Disabled array formats produce configuration errors.

## Object Parsing

| Format | Example | Binding |
| --- | --- | --- |
| `Flat` | `?brand=tesla&year=2025` | Top-level properties |
| `DotNotation` | `?filter.brand=tesla&filter.year=2025` | Nested object path |
| `DeepObject` | `?filter[brand]=tesla&filter[year]=2025` | Nested object path |

Rules:

- Flat keys bind to top-level properties.
- Dot notation and deep object notation bind nested properties.
- Duplicate scalar values use the last value.
- Nullable primitive scalar values are supported.
- Disabled object formats produce configuration errors.

## JSON Query Values

`QueryStringParser` and generated whole-request binding disable JSON query values by default and must be enabled explicitly:

```csharp
var options = new QueryParsingOptions
{
    EnableJsonQueryValues = true
};
```

Example:

```http
?filter=%7B%22brand%22%3A%22tesla%22%2C%22year%22%3A2025%7D
```

When JSON query values are disabled, JSON-looking values for complex object targets produce `DisabledJsonQueryValues`. Direct complex `[FromQuery]` parsers use `[ValiantQueryParsing]` and remain enabled when that attribute is absent.

## Serializer Configuration

The serializer has one selected array format and one selected object format.

```csharp
var options = new QuerySerializationOptions
{
    ArrayFormat = QueryArrayFormat.CommaSeparated,
    ObjectFormat = QueryObjectFormat.DotNotation
};
```

Serialization does not negotiate or infer a format from multiple supported options. It always emits the configured formats.

## Serializer Selection Algorithm

For arrays:

1. Validate that `ArrayFormat` is a defined `QueryArrayFormat` value.
2. Emit arrays using `ArrayFormat`.

For objects:

1. Validate that `ObjectFormat` is a defined `QueryObjectFormat` value.
2. Emit object paths using `ObjectFormat`.

## Array Serialization

For this value:

```csharp
new { ids = new[] { 1, 2, 3 } }
```

The serializer can emit:

| Format | Output |
| --- | --- |
| `RepeatedKeys` | `?ids=1&ids=2&ids=3` |
| `CommaSeparated` | `?ids=1,2,3` |
| `Brackets` | `?ids[]=1&ids[]=2&ids[]=3` |
| `IndexedBrackets` | `?ids[0]=1&ids[1]=2&ids[2]=3` |

Array serialization preserves source array order.

## Object Serialization

For this value:

```csharp
new
{
    filter = new
    {
        brand = "tesla",
        year = 2025
    }
}
```

The serializer can emit:

| Format | Output |
| --- | --- |
| `DotNotation` | `?filter.brand=tesla&filter.year=2025` |
| `DeepObject` | `?filter[brand]=tesla&filter[year]=2025` |

Nested objects cannot be emitted with `Flat` unless JSON query values are enabled, because flat keys do not preserve nested path information.

## Error Contract

Parsing and serialization return structured errors:

```csharp
public sealed class QueryError
{
    public string Path { get; }
    public string Code { get; }
    public string Message { get; }
    public QueryErrorKind Kind { get; }
}
```

Error kinds:

| Kind | Meaning |
| --- | --- |
| `Validation` | The query value or target shape is invalid. |
| `Configuration` | The query uses a disabled format or serialization requested a disabled or ambiguous output format. |

Common validation errors:

- Invalid integer conversion.
- Invalid GUID conversion.
- Empty array values.
- Scalar values supplied for complex object targets.
- Unsupported target shapes.

Common configuration errors:

- Disabled array format.
- Disabled object format.
- Disabled JSON query values.
- Ambiguous array output format.
- Ambiguous object output format.
- Selected output format is not enabled.

Known error codes:

| Code | Kind | Meaning |
| --- | --- | --- |
| `InvalidQueryParsingOptions` | `Configuration` | Parser options are null or contain an unknown enum value. |
| `InvalidQuerySerializationOptions` | `Configuration` | Serializer options contain an unknown enum value. |
| `DisabledQueryArrayFormat` | `Configuration` | The query uses an array format that is not enabled for parsing. |
| `DisabledQueryObjectFormat` | `Configuration` | The query uses an object format that is not enabled for parsing. |
| `DisabledJsonQueryValues` | `Configuration` | A JSON query value was used while JSON query values are disabled. |
| `InvalidQueryKey` | `Validation` | The query key is empty or uses unsupported bracket syntax. |
| `InvalidQueryValue` | `Validation` | The query value cannot be converted or cannot bind to the requested target shape. |
| `InvalidJsonQueryValue` | `Validation` | JSON query value parsing failed. |
| `UnsupportedQueryType` | `Validation` | The parser cannot create or bind the requested target type. |

## Recommended Policy

For public APIs, prefer accepting known client formats while emitting one stable canonical format:

```csharp
var parsing = new QueryParsingOptions
{
    SupportedArrayFormats =
    [
        QueryArrayFormat.RepeatedKeys,
        QueryArrayFormat.CommaSeparated,
        QueryArrayFormat.Brackets
    ],
    SupportedObjectFormats =
    [
        QueryObjectFormat.Flat,
        QueryObjectFormat.DotNotation
    ],
    EnableJsonQueryValues = false
};

var serialization = new QuerySerializationOptions
{
    ArrayFormat = QueryArrayFormat.RepeatedKeys,
    ObjectFormat = QueryObjectFormat.DotNotation
};
```

This accepts several inbound conventions but always emits:

```http
?ids=1&ids=2&ids=3&filter.brand=tesla&filter.year=2025
```
