---
title: Conditional registration
description: Include or exclude generated endpoint groups and endpoints at application startup.
---

# Conditional registration

Configure generated groups and endpoints independently:

```csharp
builder.Services.AddValiantEndpoints(options =>
{
    options.MapEndpointGroupsWhen = group =>
        group.GroupType != typeof(ExperimentalEndpointGroup) ||
        builder.Environment.IsDevelopment();

    options.MapEndpointWhen = endpoint =>
        endpoint.EndpointType != typeof(DeprecatedEndpoint);
});
```

Each predicate receives generated registration metadata while `MapValiantEndpoints()` runs at startup. Both policies allow every generated registration by default. Assigning either property again replaces its previously configured predicate.

See the
[runnable conditional-registration sample](../samples.md#conditional-registration)
for allowed and rejected group and standalone endpoint routes.

`EndpointGroupRegistration` exposes:

- `GroupType`;
- generated mapping `Order`.

`EndpointRegistration` exposes:

- `EndpointType`;
- the optional containing `Group`;
- generated mapping `Order`;
- `IsGrouped`;
- the pre-mapping `QueryParameters` snapshot.

The query snapshot reports whether binding support was generated, whether runtime binding filters are enabled, and the parsing options known before `IEndpoint.Map(...)` executes. It cannot include route patterns, HTTP methods, tags, authorization, or endpoint-authored `.WithQueryParsingOptions(...)` metadata because those are created during mapping.

For a group inferred from referenced generated endpoints, `Order` is the lowest generated child order used to place that group. Previously compiled generated registrars did not carry order metadata, so the compatibility bridge reports `0` for those legacy registrations.

## Exclude a whole group

```csharp
options.MapEndpointGroupsWhen = group =>
    group.GroupType != typeof(AdminEndpointGroup);
```

Returning `false` for a group skips:

- group creation;
- all generated endpoints assigned to that group;
- the `ConfigureEndpointGroup` hook.

The endpoint predicate is not evaluated for endpoints in an excluded group.

## Exclude individual endpoints

```csharp
options.MapEndpointWhen = endpoint =>
    endpoint.EndpointType != typeof(DeleteAccountEndpoint);
```

Returning `false` for an endpoint excludes only that grouped or standalone endpoint. Other endpoints in its group still map normally.

## Combine feature flags, environment, and metadata

```csharp
var disabled = new HashSet<Type>
{
    typeof(LegacyReportsEndpoint),
    typeof(PreviewEndpointGroup)
};

builder.Services.AddValiantEndpoints(options =>
{
    options.MapEndpointGroupsWhen = group =>
        !disabled.Contains(group.GroupType) &&
        (!group.GroupType.Name.StartsWith("Internal", StringComparison.Ordinal) ||
         builder.Environment.IsDevelopment());

    options.MapEndpointWhen = endpoint =>
        !disabled.Contains(endpoint.EndpointType) &&
        (!endpoint.EndpointType.Name.StartsWith("Internal", StringComparison.Ordinal) ||
         builder.Environment.IsDevelopment());
});
```

You can also inspect custom attributes on each `Type`, configuration, license state, or a precomputed feature snapshot. Keep the predicates fast and deterministic because they run synchronously during route construction.

## Configure parsing conditionally

Use a separate rule when selected endpoint registrations need different parsing:

```csharp
options.ConfigureEndpointWhen(
    endpoint => endpoint.Group?.GroupType == typeof(SearchEndpointGroup),
    endpointOptions => endpointOptions.QueryParameters.Parsing =
        new QueryParsingOptions
        {
            SupportedArrayFormats = [QueryArrayFormat.CommaSeparated],
            SupportedObjectFormats = [QueryObjectFormat.Flat]
        });
```

All rule predicates observe the same initial pre-mapping snapshot. Matching actions run in registration order before `MapEndpointWhen` evaluates the final descriptor. Parsing precedence is:

1. Explicit `[ValiantQueryParsing]` properties.
2. Endpoint-authored `.WithQueryParsingOptions(...)`.
3. Conditional registration configuration.
4. Global `ValiantEndpointOptions.QueryParameters.Parsing`.

`EnableBinding` remains a compilation-wide generator decision. Set `options.QueryParameters.EnableBinding = true` directly in the `AddValiantEndpoints` setup lambda when generated binding is required. Conditional parsing rules cannot generate or remove `BindAsync` and direct `TryParse` members.

## Migrate from `ShouldMap`

Split the old shared predicate by registration kind:

```csharp
// Before
options.ShouldMap = type => !disabled.Contains(type);

// After
options.MapEndpointGroupsWhen = group => !disabled.Contains(group.GroupType);
options.MapEndpointWhen = endpoint => !disabled.Contains(endpoint.EndpointType);
```

:::warning Startup decision, not request routing
The predicates are evaluated once when endpoints are mapped. They do not change routes after startup and are not a substitute for authorization or per-request feature evaluation.
:::

:::info Generated registrations only
Routes mapped directly with ASP.NET Core are outside Valiant's generated mapping policy. Apply application-specific conditions to those routes directly.
:::
