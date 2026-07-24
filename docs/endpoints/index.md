---
title: Endpoints
description: Endpoint-per-class Minimal APIs with generated registration, groups, conventions, and rich query binding.
slug: /
---

# Minimal APIs, organized by feature

Valiant Endpoints gives every ASP.NET Core Minimal API endpoint a small class and generates deterministic route registration. Group vertical slices, apply conventions once, exclude features at startup, and bind query shapes that ASP.NET Core does not handle by default.

```csharp
using Valiant.Endpoints;
using Valiant.Endpoints.AspNetCore;

[ValiantEndpoint<TodosEndpointGroup>]
internal sealed class ListTodosEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet("/", () => Results.Ok());
}
```

```csharp
builder.Services.AddValiantEndpoints();

var app = builder.Build();
app.MapValiantEndpoints();
```

## What it covers

| Capability | API |
| --- | --- |
| Generated discovery | `[ValiantEndpoint]`, `[ValiantEndpoint<TGroup>]`, `[ValiantEndpointGroup]` |
| Route organization | `IEndpoint`, `IEndpointGroup`, standalone and grouped routes |
| Shared policy | Group and endpoint configuration hooks over the returned route builders |
| Feature switches | Separate startup predicates for generated groups and endpoints |
| Multiple assemblies | Explicit registration and mapping overloads |
| Query compatibility | Arrays, nested objects, JSON opt-in, parser, serializer, and structured errors |
| Request binding | Opt-in generated `BindAsync`, `[AsParameters]` repair, and complex `[FromQuery]` shims; native ASP.NET Core binding by default |

## Documentation map

- [Quick start](./quick-start.md)
- [Runnable samples](./samples.md)
- [Groups and conventions](./guides/groups-conventions.md)
- [Conditional registration](./guides/conditional-registration.md)
- [Request binding](./guides/request-binding.md)
- [Query parameter reference](./reference/query-parameters.md)
- [Registration, generation, and diagnostics](./reference/registration-generation.md)

The [sample catalog](./samples.md) covers every selectable startup in the
runnable endpoint-per-class host, including generated binding and intentional
conditional exclusions.
