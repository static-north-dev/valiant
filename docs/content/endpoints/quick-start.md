---
title: Quick start
description: Install StaticNorth.Valiant.Endpoints.AspNetCore and map your first generated endpoint.
---

# Quick start

## 1. Install

```bash
dotnet add package StaticNorth.Valiant.Endpoints.AspNetCore
```

This is the consumer package. It includes the shared endpoint contracts and bundles the source generator; do not add separate `StaticNorth.Valiant.Endpoints` or generator references.

## 2. Create an endpoint

```csharp
using StaticNorth.Valiant.Endpoints;
using StaticNorth.Valiant.Endpoints.AspNetCore;

[ValiantEndpoint]
internal sealed class HealthEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet("/health", () => Results.Ok(new { status = "ready" }));
    }
}
```

`[ValiantEndpoint]` makes it a standalone endpoint on the root route builder.

## 3. Register and map

```csharp
using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;

builder.Services.AddValiantEndpoints();

var app = builder.Build();
app.MapValiantEndpoints();
app.Run();
```

Registration discovers generated registrar metadata from the calling and entry assemblies. Mapping resolves those registrars and invokes each generated route plan.

## 4. Put endpoints in a group

```csharp
[ValiantEndpointGroup]
internal sealed class TodosEndpointGroup : IEndpointGroup
{
    public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) =>
        builder.MapGroup("/api/todos").WithTags("Todos");
}

[ValiantEndpoint<TodosEndpointGroup>]
internal sealed class GetTodoEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapGet("/{id:guid}", (Guid id) => Results.Ok(id));
}
```

Every endpoint group must implement `CreateGroup`; Valiant does not derive group routes from type names.

Next: centralize tags, filters, OpenAPI, and naming in [Groups and conventions](./guides/groups-conventions.md).
