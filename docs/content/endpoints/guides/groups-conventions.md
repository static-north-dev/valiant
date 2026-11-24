---
title: Groups and conventions
description: Create route groups, order registration, and apply global conventions.
---

# Groups and conventions

## Create groups

Every endpoint group implements `CreateGroup` and explicitly chooses its route and group-level metadata:

```csharp
[ValiantEndpointGroup(Order = 10)]
internal sealed class OrdersEndpointGroup : IEndpointGroup
{
    public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) =>
        builder
            .MapGroup("/api/orders")
            .WithTags("Orders")
            .RequireAuthorization();
}
```

## Global conventions

```csharp
builder.Services.AddValiantEndpoints(options =>
{
    options.ConfigureEndpointGroup = (group, registration) =>
    {
        group.AddEndpointFilter<ExceptionHandlingFilter>();
    };

    options.ConfigureEndpoint = (endpoint, registration) =>
        endpoint.WithName(registration.EndpointType.Name);
});
```

The generated sequence is:

1. Create the group through its required `CreateGroup` implementation.
2. Associate its `EndpointGroupRegistration`.
3. Run `ConfigureEndpointGroup`.
4. Map each included endpoint and apply Valiant binding and parsing conventions.
5. Run `ConfigureEndpoint` with that endpoint's `EndpointRegistration`.

Both hooks are actions over the builders returned by `IEndpointGroup.CreateGroup(...)` and `IEndpoint.Map(...)`. The group hook cannot replace the returned builder. Put route prefixes and nested `MapGroup(...)` calls in the group's `CreateGroup` implementation.

`EndpointGroupRegistration` exposes the group type and generated order. `EndpointRegistration` exposes the endpoint type, containing group, generated order, and query-binding snapshot, so global conventions can branch without repeating reflection-based discovery.

See the
[runnable configuration sample](../samples.md#configuration-hooks)
for two grouped routes where a group hook adds a shared response header and an endpoint hook adds a second header only to the selected endpoint.

## Ordering

Set `Order` on endpoint and group marker attributes when one generated mapping must precede another:

```csharp
[ValiantEndpoint<OrdersEndpointGroup>(Order = -100)]
internal sealed class SearchOrdersEndpoint : IEndpoint { /* ... */ }
```

Within each registered assembly, Valiant creates the groups first. On .NET 11 and later targets, it then sorts grouped and standalone endpoints together by `Order` and type name. On earlier targets, it keeps the 1.2.1 order and maps the grouped endpoints before the standalone endpoints.

Set `options.OrderEndpointsAcrossGroups` in `AddValiantEndpoints` to override the target's default. This setting controls the order of startup callbacks. Use ASP.NET Core's `.WithOrder()` to set route matching priority.

## Standalone endpoints

`[ValiantEndpoint]` without a group maps directly on the supplied root route builder. The same `ConfigureEndpoint` hook and `MapEndpointWhen` registration policy apply; its registration has `Group == null`.

See [Upgrading Endpoints from preview](./upgrading-from-preview.md) for the earlier convention hooks.
