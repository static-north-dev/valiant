# Valiant Endpoints for ASP.NET Core

Valiant Endpoints provides endpoint-per-class helpers for ASP.NET Core Minimal APIs. It is designed for vertical-slice style endpoint organization with source-generated route registration.

## Package

- `StaticNorth.Valiant.Endpoints.AspNetCore` provides endpoint contracts, route mapping helpers, dependency injection integration, and the bundled source generator.

`StaticNorth.Valiant.Endpoints` contains shared endpoint metadata primitives used by the ASP.NET Core package. It is bundled inside `StaticNorth.Valiant.Endpoints.AspNetCore` and is not resolved as a separate NuGet package by consumers.

## Quick Start

```csharp
using StaticNorth.Valiant.Endpoints;
using StaticNorth.Valiant.Endpoints.AspNetCore;

[ValiantEndpointGroup]
internal sealed class TodosEndpointGroup : IEndpointGroup
{
    public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) =>
        builder.MapGroup("/todos").WithTags("Todos");
}

[ValiantEndpoint<TodosEndpointGroup>]
internal sealed class ListTodosEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet("/", () => Results.Ok());
    }
}
```

Register and map generated endpoints:

```csharp
builder.Services.AddValiantEndpoints();

var app = builder.Build();

app.MapValiantEndpoints();
```

Configure shared endpoint conventions at registration time:

```csharp
builder.Services.AddValiantEndpoints(options =>
{
    options.ConfigureEndpointGroup = (group, registration) =>
    {
        group.AddEndpointFilter(new ExceptionHandlingFilter());
        group.WithOpenApi();
    };

    options.ConfigureEndpoint = (endpoint, registration) =>
        endpoint.WithName(registration.EndpointType.Name);

    options.MapEndpointGroupsWhen = group =>
        group.GroupType != typeof(ExperimentalEndpointGroup) ||
        builder.Environment.IsDevelopment();

    options.MapEndpointWhen = endpoint =>
        endpoint.EndpointType != typeof(DeprecatedEndpoint);
});
```

[The runnable conditional-registration sample](samples/StaticNorth.Valiant.Endpoints.AspNetCore.Samples/ConditionalRegistration)
shows allowed and rejected routes for both predicates.

[The runnable configuration sample](samples/StaticNorth.Valiant.Endpoints.AspNetCore.Samples/Configuration)
makes both hook scopes visible through response headers.

Both predicates are optional and allow every generated registration by default. Group registrations expose `GroupType` and `Order`. Endpoint registrations expose `EndpointType`, the optional containing `Group`, `Order`, and a pre-mapping query binding snapshot. They are evaluated while `MapValiantEndpoints()` runs during application startup. Returning `false` from `MapEndpointGroupsWhen` prevents the group, its generated endpoints, and group conventions from being mapped; the endpoint predicate is not evaluated for those skipped children. Returning `false` from `MapEndpointWhen` excludes only that grouped or standalone endpoint. Assigning either property again replaces its previous predicate. Routes mapped directly with ASP.NET Core are outside these policies.

Use `ConfigureEndpointWhen` for conditional registration-level parsing:

```csharp
options.ConfigureEndpointWhen(
    endpoint => endpoint.Group?.GroupType == typeof(SearchEndpointGroup),
    endpointOptions => endpointOptions.QueryParameters.Parsing =
        new QueryParsingOptions
        {
            SupportedArrayFormats = [QueryArrayFormat.CommaSeparated]
        });
```

Parsing precedence is `[ValiantQueryParsing]`, endpoint-authored `.WithQueryParsingOptions(...)`, conditional registration, then global options. Query binding generation remains a compilation-wide opt-in through a direct `options.QueryParameters.EnableBinding = true` assignment.

Endpoint generation itself is attribute-scoped. The companion missing-marker analyzer reports direct `IEndpoint` and `IEndpointGroup` declarations without the corresponding marker attribute, but intentionally does not scan indirect or aliased interface implementations to keep IDE analysis bounded.

## Native AOT

Generated registration without query-binding opt-in uses a mapping path that does not reference the reflection-based query binder. Group/endpoint policies and conventions still apply. Registrar metadata preserves the constructors needed by automatic dependency injection registration. The packed Native AOT smoke application exercises both grouped and standalone endpoints through `AddValiantEndpoints(typeof(Program).Assembly)` and `MapValiantEndpoints()`. Pass endpoint assemblies explicitly in Native AOT: the parameterless and setup-only overloads use `Assembly.GetCallingAssembly()`, which the native runtime does not support, and are annotated with `RequiresDynamicCode`.

Enabling Valiant query binding remains a separate feature; it does not inherit this query-disabled registration guarantee. Existing query-enabled and previously generated registrations retain their runtime behavior.

## Query Parsing and Serialization

Valiant includes a configurable query parser and serializer for API clients that use different array and object query string conventions. The parser and serializer use separate option types because inbound compatibility and outbound representation are different concerns:

- Parsing controls which formats clients are allowed to send.
- Serialization controls which format Valiant emits when it builds query strings.

This lets an API accept multiple client conventions without changing the stable outbound format used for generated links, redirects, or client requests.

See [Query Parameter Parsing and Serialization](docs/query-parameters.md) for the full technical reference, including format detection, serializer selection rules, supported target types, and error codes.

Valiant query binding is disabled by default, so endpoints use native ASP.NET Core binding unless the current endpoint project opts in with a direct compile-time `true` assignment:

```csharp
builder.Services.AddValiantEndpoints(options =>
{
    options.QueryParameters.EnableBinding = true;
});
```

Changing this assignment rebuilds the project and adds or removes the generated `BindAsync` and `TryParse` members. The assignment must appear directly in the `AddValiantEndpoints` setup lambda in the same compilation as the endpoint types; helper methods, runtime variables, and configuration in a separate host assembly cannot control source emitted into a previously compiled endpoint assembly.

This is a compile-time opt-in, not a fully dynamic runtime switch. If the generator sees a recognized literal `true`, the generated members remain in the compiled assembly. Whole-request `BindAsync` and `[AsParameters]` processing can read runtime options, but a direct generated `TryParse(string, out T)` has no `HttpContext` and cannot observe a later runtime override to `false`. Do not compile an endpoint assembly with a recognized `true` assignment and then expect another host or runtime override to disable `[ValiantQueryParsing]`; compile that endpoint project with `EnableBinding = false` so the parser is not emitted.

### Defaults

Parser and serializer defaults are conservative:

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

With those defaults, these formats are accepted or emitted:

```http
/items?ids=1&ids=2&ids=3
/items?brand=tesla&year=2025
```

These formats are rejected unless explicitly enabled:

```http
/items?ids=1,2,3
/items?ids[]=1&ids[]=2
/items?ids[0]=1&ids[1]=2
/items?filter.brand=tesla
/items?filter[brand]=tesla
/items?filter=%7B%22brand%22%3A%22tesla%22%7D
```

### Configure Endpoint Policy

Configure query policy during endpoint registration when you want one application-wide convention:

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
        ],
        EnableJsonQueryValues = false
    };

    options.QueryParameters.Serialization = new QuerySerializationOptions
    {
        ArrayFormat = QueryArrayFormat.RepeatedKeys,
        ObjectFormat = QueryObjectFormat.DotNotation
    };
});
```

An endpoint can completely replace the global parsing rules used by generated whole-request binding and `[AsParameters]` binding:

```csharp
return builder.MapGet("/search", Handle)
    .WithQueryParsingOptions(new QueryParsingOptions
    {
        SupportedObjectFormats = [QueryObjectFormat.Flat],
        EnableJsonQueryValues = true
    });
```

The last endpoint-local call wins. Endpoints without local metadata use the global parsing options; endpoint-local options are inert when Valiant query binding is disabled.

Use `[ValiantQueryParsing]` on an endpoint class when only selected parsing settings should differ:

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

Attribute properties are partial overrides. An omitted or explicit `null` array inherits the lower-precedence value; an empty array accepts none of that format category. Resolution order is:

1. Explicit `[ValiantQueryParsing]` properties.
2. The last `.WithQueryParsingOptions(...)` call.
3. `ValiantEndpointOptions.QueryParameters.Parsing`.
4. `QueryParsingOptions` defaults.

`SupportedArrayFormats` and `SupportedObjectFormats` apply only to generated whole-request and `[AsParameters]` binding. `JsonValues` also controls generated direct complex `[FromQuery]` parsers. For full-query binding, `JsonValues` participates in the override only when it is explicitly named; for direct binding, omission preserves the existing JSON-enabled behavior.

With that parsing configuration, these requests are accepted:

```http
/items?ids=1&ids=2&ids=3
/items?ids=1,2,3
/items?filter.brand=tesla&filter.year=2025
```

These requests are rejected with structured configuration errors:

```http
/items?ids[]=1&ids[]=2
/items?ids[0]=1&ids[1]=2
/items?filter[brand]=tesla&filter[year]=2025
/items?filter=%7B%22brand%22%3A%22tesla%22%7D
```

### Bind Endpoint Requests

After enabling Valiant query binding, endpoints that need nested query formats can use a request type with `[FromQuery]` query properties.

Without `[AsParameters]`, make the request type partial. The source generator emits `BindAsync(HttpContext, ParameterInfo)` on the request type:

```csharp
return builder.MapGet("/cars", static (ListCarsRequest request) =>
{
    // request.Filter binds from ?filter.id=1 or ?filter[id]=1 when those formats are enabled.
});

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

For a nested request type, declare the request and every containing type `partial` so the generator can reopen the declaration chain.

The source generator emits `BindAsync(HttpContext, ParameterInfo)` for the request type and delegates to `QueryStringParser` with `ValiantEndpointOptions.QueryParameters.Parsing`.

With `[AsParameters]`, Valiant adds an endpoint filter after mapping. ASP.NET still creates the request object, then the Valiant filter reparses the full query string with `QueryStringParser` and copies configured `[FromQuery]` properties back onto the request object:

```csharp
return builder.MapGet("/cars", static ([AsParameters] ListCarsRequest request) =>
{
    // request.Filter binds from ?filter.id=1 or ?filter[id]=1 when those formats are enabled.
});

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

For `[AsParameters]`, complex `[FromQuery]` property types still need the generated `TryParse(string, out T)` compatibility method because ASP.NET validates property bindability before endpoint filters run. That is why the nested `CarFilter` type is partial in this shape.

The filter runs after ASP.NET Core's native property binding, so it cannot repair a format that causes the initial property conversion to fail. For example, comma-separated input such as `?ids=1,2,3` fails native binding for an `int[] Ids` property before the Valiant filter runs. Use generated whole-request `BindAsync` binding when every configured array and object format must work independently of native `[AsParameters]` property binding.

Direct complex `[FromQuery]` parameters use a generated `TryParse(string, out T)` method. Configure that generated parser declaratively on the endpoint:

```csharp
[ValiantEndpoint]
[ValiantQueryParsing(JsonValues = JsonQueryValues.Disabled)]
public sealed class SearchEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet("/search", static ([FromQuery] SearchRequest request) => Results.Ok(request));
}

public sealed partial record SearchRequest(string? Term);
```

The choice is compiled into `TryParse`; direct binding does not use an endpoint filter. The attribute is ignored when the endpoint project is compiled with `options.QueryParameters.EnableBinding = false` because no Valiant query parser is generated. A later runtime-only override cannot change an already-generated direct parser, as described in the compile-time opt-in note above. When binding is enabled, no attribute, `[ValiantQueryParsing]`, and explicit `Enabled` all preserve JSON binding. Explicit `Disabled` returns HTTP 400. `SupportedArrayFormats` and `SupportedObjectFormats` do not affect direct parameters. A user-authored `TryParse` remains user-controlled. If the same partial type is reused by direct parameters with conflicting policies, generation reports `VLE011`.

### Migrating Existing Query Binding

Query binding now defaults to native ASP.NET Core behavior. Existing applications that rely on Valiant-generated `BindAsync`, complex `TryParse` shims, or `[AsParameters]` repair must add `options.QueryParameters.EnableBinding = true` directly to their `AddValiantEndpoints` setup lambda.

### Parse Directly

Use `QueryStringParser.Parse<T>` when parsing a query string directly:

```csharp
var result = QueryStringParser.Parse<SearchQuery>(
    "?ids=1,2,3&filter.brand=tesla&filter.year=2025",
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
        // error.Path, error.Code, error.Message, error.Kind
    }
}
```

The parser supports arrays and `List<T>` for these primitive types:

- `string`
- `int`
- `long`
- `Guid`
- `bool`
- `decimal`
- `DateTime`

Nullable primitive scalar values are also supported. Nested object binding is supported at least one level deep.

### Array Parsing Formats

Enable each array format explicitly through `QueryParsingOptions.SupportedArrayFormats`.

```csharp
var parsing = new QueryParsingOptions
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

Supported array formats:

```http
# QueryArrayFormat.RepeatedKeys
/items?ids=1&ids=2&ids=3

# QueryArrayFormat.CommaSeparated
/items?ids=1,2,3

# QueryArrayFormat.Brackets
/items?ids[]=1&ids[]=2&ids[]=3

# QueryArrayFormat.IndexedBrackets
/items?ids[0]=1&ids[1]=2&ids[2]=3
```

Array parsing rules:

- Repeated keys preserve the original query order.
- Comma-separated values are split after URL decoding.
- Bracket arrays preserve the original query order.
- Indexed bracket arrays are sorted by numeric index.
- Empty array elements, such as `?ids=1,,3`, return validation errors.
- A disabled array format returns a configuration error instead of being silently parsed.

### Object Parsing Formats

Enable each object format explicitly through `QueryParsingOptions.SupportedObjectFormats`.

```csharp
var parsing = new QueryParsingOptions
{
    SupportedObjectFormats =
    [
        QueryObjectFormat.Flat,
        QueryObjectFormat.DotNotation,
        QueryObjectFormat.DeepObject
    ]
};
```

Supported object formats:

```http
# QueryObjectFormat.Flat
/items?brand=tesla&year=2025

# QueryObjectFormat.DotNotation
/items?filter.brand=tesla&filter.year=2025

# QueryObjectFormat.DeepObject
/items?filter[brand]=tesla&filter[year]=2025
```

Object parsing rules:

- Flat keys bind to top-level properties.
- Dot notation creates nested object paths.
- Deep object bracket notation creates nested object paths.
- Duplicate scalar values use the last value.
- Disabled object formats return configuration errors.

### JSON Query Values

`QueryStringParser` and generated whole-request binding disable JSON query values by default. Enable them only when an endpoint intentionally accepts JSON-encoded object values in the query string:

```csharp
var parsing = new QueryParsingOptions
{
    EnableJsonQueryValues = true
};
```

```http
/items?filter=%7B%22brand%22%3A%22tesla%22%2C%22year%22%3A2025%7D
```

When `EnableJsonQueryValues` is `false`, JSON-looking values for complex objects return a `DisabledJsonQueryValues` configuration error. Direct complex `[FromQuery]` parsers are separately controlled by `[ValiantQueryParsing]` and preserve their existing enabled behavior when the attribute is absent.

### Serialize Directly

Use `QueryStringSerializer.Serialize` to write a query string:

```csharp
var result = QueryStringSerializer.Serialize(
    new
    {
        ids = new[] { 1, 2, 3 },
        filter = new
        {
            brand = "tesla",
            year = 2025
        }
    },
    new QuerySerializationOptions
    {
        ArrayFormat = QueryArrayFormat.CommaSeparated,
        ObjectFormat = QueryObjectFormat.DotNotation
    });

// result.QueryString == "?ids=1,2,3&filter.brand=tesla&filter.year=2025"
```

The serializer URL-encodes keys and values, preserves array order, and skips null values.

### Serializer Format Selection

Serialization has one selected array format and one selected object format:

Selection rules:

- `ArrayFormat` selects how arrays are emitted.
- `ObjectFormat` selects how object paths are emitted.
- Defaults are `QueryArrayFormat.RepeatedKeys` and `QueryObjectFormat.Flat`.
- Serialization does not negotiate or infer a format from multiple supported options. It always emits the configured formats.
- Invalid enum values return a configuration error.

This configuration emits comma-separated arrays:

```csharp
var options = new QuerySerializationOptions
{
    ArrayFormat = QueryArrayFormat.CommaSeparated
};
```

This emits:

```http
?ids=1,2,3
```

### Array Serialization Formats

```http
# QueryArrayFormat.RepeatedKeys
?ids=1&ids=2&ids=3

# QueryArrayFormat.CommaSeparated
?ids=1,2,3

# QueryArrayFormat.Brackets
?ids[]=1&ids[]=2&ids[]=3

# QueryArrayFormat.IndexedBrackets
?ids[0]=1&ids[1]=2&ids[2]=3
```

### Object Serialization Formats

```http
# QueryObjectFormat.DotNotation
?filter.brand=tesla&filter.year=2025

# QueryObjectFormat.DeepObject
?filter[brand]=tesla&filter[year]=2025
```

Nested objects cannot be emitted with `QueryObjectFormat.Flat` unless JSON query values are enabled, because flat query keys do not carry enough path information for nested object structure.

### Structured Errors

Parser and serializer failures are returned as structured `QueryError` entries:

```csharp
public sealed class QueryError
{
    public string Path { get; }
    public string Code { get; }
    public string Message { get; }
    public QueryErrorKind Kind { get; }
}
```

`QueryErrorKind.Validation` means the query value or target shape is invalid. Examples include invalid integer conversion, invalid GUID conversion, empty array values, and unsupported target types.

`QueryErrorKind.Configuration` means the query uses a disabled format or serialization requested a disabled or ambiguous output format. Examples include disabled bracket arrays, disabled dot notation, disabled JSON query values, and missing `ArrayFormat` when multiple array output formats are enabled.

### Separate Parse and Serialize Policies

A typical production policy accepts multiple input formats but emits one stable format:

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
    ]
};

var serialization = new QuerySerializationOptions
{
    ArrayFormat = QueryArrayFormat.RepeatedKeys,
    ObjectFormat = QueryObjectFormat.DotNotation
};
```

## Groups

Endpoint groups are marker types with one required factory:

- `CreateGroup(...)` creates the route group and applies group-level conventions.

```csharp
[ValiantEndpointGroup]
internal sealed class TodosEndpointGroup : IEndpointGroup
{
    public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder)
    {
        return builder
            .MapGroup("/todos")
            .WithTags("Todos");
    }
}
```

Every endpoint group must implement `CreateGroup`. Valiant does not derive group routes from type names.

When migrating from convention-derived routes, move the prefix and route name into each group. For example, replace a default `/api/Orders` route with `builder.MapGroup("/api/orders")` in `OrdersEndpointGroup.CreateGroup`.

Endpoints opt into a group with `[ValiantEndpoint<TodosEndpointGroup>]` or `[ValiantEndpoint(typeof(TodosEndpointGroup))]`. Endpoint group classes use `[ValiantEndpointGroup]`, so the generator can validate that endpoints and groups use the correct marker.

The group and endpoint mapping order is:

1. The group's `CreateGroup(...)` implementation.
2. Associate its `EndpointGroupRegistration` and run `ConfigureEndpointGroup`.
3. Map each included generated endpoint and apply Valiant binding and parsing conventions.
4. Run `ConfigureEndpoint` with that endpoint's `EndpointRegistration`.

`ConfigureEndpointGroup` and `ConfigureEndpoint` are actions over the builders returned by the group and endpoint implementations. Route prefixes and nested groups belong in `IEndpointGroup.CreateGroup`; the global group hook does not replace the returned `RouteGroupBuilder`.

## Standalone Endpoints

Use `[ValiantEndpoint]` without a group type to map an endpoint at the root route builder:

```csharp
[ValiantEndpoint]
internal sealed class HealthEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet("/health", () => Results.Ok());
    }
}
```

## Ordering

Use `Order` when generated mapping order matters:

```csharp
[ValiantEndpoint<TodosEndpointGroup>(Order = 10)]
internal sealed class ListTodosEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet("/", () => Results.Ok());
    }
}
```

Endpoints with the same order are sorted by type name for deterministic generated output.
