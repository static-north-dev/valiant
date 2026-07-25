---
title: Samples
description: Run the endpoint-per-class, configuration, conditional registration, and query-binding samples.
---

# Endpoint samples

The runnable host has four startup choices. Select one near the top of
[`Program.cs`](https://github.com/ignitea-dev/valiant/blob/main/endpoints/samples/StaticNorth.Valiant.Endpoints.AspNetCore.Samples/Program.cs),
then run:

```bash
dotnet run --project endpoints/samples/StaticNorth.Valiant.Endpoints.AspNetCore.Samples
```

```csharp
Type? startup;

//startup = typeof(Samples.Basic.Startup);
//startup = typeof(Samples.Configuration.Startup);
//startup = typeof(Samples.ConditionalRegistration.Startup);
startup = typeof(Samples.QueryBinding.Startup);
```

Endpoint discovery is assembly-wide. Every attributed endpoint in the sample
project contributes generated registration code; the selected startup controls
the global options and conventions applied when those registrations are mapped.

## Available startups

| Startup | Demonstrates | Source |
| --- | --- | --- |
| `Basic.Startup` | Default discovery and mapping for a standalone endpoint and an endpoint group | [`Basic`](https://github.com/ignitea-dev/valiant/tree/main/endpoints/samples/StaticNorth.Valiant.Endpoints.AspNetCore.Samples/Basic) |
| `Configuration.Startup` | `ConfigureEndpointGroup`, `ConfigureEndpoint`, and descriptor-based guards using group and endpoint metadata | [`Configuration`](https://github.com/ignitea-dev/valiant/tree/main/endpoints/samples/StaticNorth.Valiant.Endpoints.AspNetCore.Samples/Configuration) |
| `ConditionalRegistration.Startup` | Independent `MapEndpointGroupsWhen` and `MapEndpointWhen` policies, including allowed and rejected registrations | [`ConditionalRegistration`](https://github.com/ignitea-dev/valiant/tree/main/endpoints/samples/StaticNorth.Valiant.Endpoints.AspNetCore.Samples/ConditionalRegistration) |
| `QueryBinding.Startup` | Generated query binding for arrays, objects, JSON values, and supported query formats | [`QueryBinding`](https://github.com/ignitea-dev/valiant/tree/main/endpoints/samples/StaticNorth.Valiant.Endpoints.AspNetCore.Samples/QueryBinding) |

Every startup registers and maps the generated route plan through
`AddValiantEndpoints()` and `MapValiantEndpoints()`.

## Configuration hooks

The configuration startup applies one convention to both endpoints in its group
and a second convention only to `SelectedEndpoint`:

| Route | `X-Valiant-Group` | `X-Valiant-Endpoint` |
| --- | --- | --- |
| `/configuration/selected` | `ConfigurationEndpointGroup` | `SelectedEndpoint` |
| `/configuration/group-only` | `ConfigurationEndpointGroup` | not present |

The hook receives the builder returned by the user-authored mapping method and
its generated registration descriptor. See
[Groups and conventions](./guides/groups-conventions.md) for the complete
mapping sequence.

## Conditional registration

The conditional startup exercises positive and negative cases for both
predicates:

| Route | Status | Policy demonstrated |
| --- | --- | --- |
| `/conditional/groups/allowed/hello` | `200` | group allowed |
| `/conditional/groups/blocked/hello` | `404` | group rejected |
| `/conditional/endpoints/allowed` | `200` | standalone endpoint allowed |
| `/conditional/endpoints/blocked` | `404` | standalone endpoint rejected |

Rejecting a group skips its child registrations, so `MapEndpointWhen` is not
evaluated for those children. These are startup-time registration decisions,
not request-time feature checks. See
[Conditional registration](./guides/conditional-registration.md) for descriptor
details and precedence.

## Query binding

The query-binding startup opts the compilation into generated binding:

```csharp
services.AddValiantEndpoints(options =>
{
    options.QueryParameters.EnableBinding = true;
});
```

Its `/query/*` routes cover array and object formats, direct JSON query values,
and an endpoint that explicitly disables JSON parsing. See
[Request binding](./guides/request-binding.md) and the
[query parameter reference](./reference/query-parameters.md) before choosing a
binding shape.
