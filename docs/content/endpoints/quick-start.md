---
title: Quick start
description: Install StaticNorth.Valiant.Endpoints.AspNetCore and map your first generated endpoint.
---

# Quick start

## 1. Create a project and install Valiant

Use the **.NET 10.0.400 SDK or newer** to build this example with Valiant 1.2.1. The project can target .NET 8. Run `dotnet --version` from the project directory to check the selected SDK. A `CS9057` warning indicates that the selected compiler is too old to load the generator.

```bash
dotnet new web -n EndpointsDemo -f net8.0
cd EndpointsDemo
dotnet add package StaticNorth.Valiant.Endpoints.AspNetCore --version 1.2.1
```

This is the consumer package. It includes the shared endpoint contracts and bundles the source generator; do not add separate `StaticNorth.Valiant.Endpoints` or generator references.

## 2. Create an endpoint

```csharp title="HealthEndpoint.cs"
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

Replace the contents of `Program.cs`:

```csharp title="Program.cs"
using StaticNorth.Valiant.Endpoints.AspNetCore;
using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddValiantEndpoints();

var app = builder.Build();
app.MapValiantEndpoints();
app.Run();
```

Run `dotnet run --no-launch-profile --urls http://localhost:5075`. Request `GET http://localhost:5075/health` to see HTTP 200 with `{"status":"ready"}`.

Registration discovers generated registrar metadata from the calling and entry assemblies. Mapping resolves those registrars and invokes each generated route plan.

For Native AOT, pass endpoint assemblies explicitly: `builder.Services.AddValiantEndpoints(typeof(HealthEndpoint).Assembly)`. See [Native AOT registration](./reference/registration-generation.md#native-aot).

## 4. Put endpoints in a group

```csharp title="TodosEndpoints.cs"
using StaticNorth.Valiant.Endpoints;
using StaticNorth.Valiant.Endpoints.AspNetCore;

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
