---
title: Upgrading Endpoints from preview
description: Update endpoint groups, convention hooks, and startup predicates from earlier Valiant previews.
---

# Upgrading Endpoints from preview

## Set group routes explicitly

Implement `IEndpointGroup.CreateGroup` and set the route prefix with `builder.MapGroup("/api/orders")`. See [Groups and conventions](./groups-conventions.md).

## Replace preview convention hooks

The earlier preview exposed three callbacks. Consolidate them as follows:

- Move both `ConfigureAllEndpointGroupsBeforeMapping` and `ConfigureAllEndpointGroupsAfterMapping` conventions into `ConfigureEndpointGroup`.
- Replace `ConfigureAllEndpointsAfterMapping` with `ConfigureEndpoint`.
- Replace callback `Type` parameters with `registration.GroupType` or `registration.EndpointType`.
- Move any callback that returned `group.MapGroup(...)` into `IEndpointGroup.CreateGroup(...)`.

## Migrate from `ShouldMap`

Split the old shared predicate by registration kind:

```csharp
// Before
options.ShouldMap = type => !disabled.Contains(type);

// After
options.MapEndpointGroupsWhen = group => !disabled.Contains(group.GroupType);
options.MapEndpointWhen = endpoint => !disabled.Contains(endpoint.EndpointType);
```
