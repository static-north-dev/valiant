---
title: Registration, generation, and diagnostics
description: Assembly registration, Native AOT, generated mapping, ordering, and diagnostics.
---

# Registration, generation, and diagnostics

## Default discovery

`AddValiantEndpoints()` registers generated route registrars from the calling and entry assemblies. `MapValiantEndpoints()` resolves every registered registrar.

## Explicit assemblies

Use explicit overloads when endpoints live in feature libraries:

```csharp
builder.Services.AddValiantEndpoints(
    typeof(OrdersEndpoint).Assembly,
    typeof(BillingEndpoint).Assembly);

app.MapValiantEndpoints();
```

Explicit assembly selection changes registration only. Endpoint mapping always uses the same `MapValiantEndpoints()` entry point, so generated mapping policy and conventions have one consistent execution path.

## Native AOT

Select endpoint assemblies explicitly with `AddValiantEndpoints(typeof(OrdersEndpoint).Assembly)`, then call `MapValiantEndpoints()` as usual. The default and setup-only registration overloads use `Assembly.GetCallingAssembly()`, which Native AOT does not support; these overloads carry `RequiresDynamicCode` and report `IL3050` during AOT analysis.

When query binding is not opted into at compile time, generated registration calls a mapping path that never references the reflection-based query binder. Mapping policies, conditional registration, and conventions still apply. Generated registrar metadata preserves its public constructors for dependency injection. The packed native smoke test covers both standalone and grouped endpoints through this registration path.

Query-binding opt-in retains the existing runtime binder and is not covered by this Native AOT guarantee. ASP.NET Core handler binding and serialization must also be AOT-compatible; use named handler method groups and generated JSON metadata for your payloads.

## Generator scope

The incremental generator starts from marker attributes—not an assembly-wide interface scan:

- `[ValiantEndpoint]` or `[ValiantEndpoint<TGroup>]` generates endpoint registration;
- `[ValiantEndpointGroup]` generates group registration;
- partial query request/type declarations receive binders only when query binding is explicitly enabled and they are referenced from generated endpoint methods.

The companion analyzer checks only direct `IEndpoint` and `IEndpointGroup` declarations for missing markers. It intentionally avoids indirect and aliased implementation scans to keep IDE analysis bounded.

`IEndpointGroup.CreateGroup` is a static abstract interface member. The C# compiler requires every endpoint group type to provide a public static implementation.

## Diagnostics

| ID | Meaning |
| --- | --- |
| `VLE001` | Marked endpoint does not implement `IEndpoint` |
| `VLE002` | A declaration implements both endpoint interfaces |
| `VLE003` | Endpoint marker was applied to a group |
| `VLE004` | Declared group type does not implement `IEndpointGroup` |
| `VLE005` | Marked group does not implement `IEndpointGroup` |
| `VLE006` | Direct endpoint declaration is missing `[ValiantEndpoint]` |
| `VLE007` | Direct group declaration is missing `[ValiantEndpointGroup]` |
| `VLE008` | Group marker was applied to an endpoint |
| `VLE009` | An endpoint uses more than one endpoint marker form |
| `VLE010` | A query type cannot receive the generated binder |

`VLE010` covers shapes such as non-partial source types, static types, open generics, or containing types that cannot receive generated members.
