---
name: valiant-endpoints
description: Build endpoint-per-class Minimal APIs with StaticNorth.Valiant.Endpoints.AspNetCore, including endpoint groups, generated registration, conventions, and request binding.
---

# Valiant Endpoints

Match the application's package major to [the documentation index](https://valiant.staticnorth.dev/agents/documents.json). Select that version explicitly. A preview may also list `current`. Read its quick start and the guide for the feature being changed.

Latest published references: [Quick start](https://valiant.staticnorth.dev/endpoints/quick-start), [Groups and conventions](https://valiant.staticnorth.dev/endpoints/guides/groups-conventions), and [Request binding](https://valiant.staticnorth.dev/endpoints/guides/request-binding).

- Install `StaticNorth.Valiant.Endpoints.AspNetCore`. It includes the contracts and generator; do not prescribe a separate `StaticNorth.Valiant.Endpoints` consumer package.
- A standalone endpoint implements `IEndpoint`, has `[ValiantEndpoint]`, and declares `public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)`.
- An endpoint group implements `IEndpointGroup`, has `[ValiantEndpointGroup]`, and declares `public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder)`. Group routes come from this method, not the class name.
- Associate an endpoint with its group using `[ValiantEndpoint<TGroup>]`.
- Add generated services with `builder.Services.AddValiantEndpoints()` from `StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection`. Map them with `app.MapValiantEndpoints()` from `StaticNorth.Valiant.Endpoints.AspNetCore`. For Native AOT, pass the endpoint assemblies explicitly and leave generated query binding disabled.
- Keep mapping and registration explicit. Preserve the application's authorization, validation, filters, route names, and error contracts when moving handlers into endpoint classes.
- For conditional registration or rich query binding, use the matching version's documented conventions and attributes rather than inventing ASP.NET binding signatures.

Verify the affected HTTP behavior using the application's existing integration-test host, including binding failures and authorization where relevant. Treat a successful build as source-generation validation, not proof of route behavior.
