---
title: Runtime, generation, and diagnostics
description: Mapping modes, generator scope, generated services, diagnostics, and known constraints.
---

# Runtime, generation, and diagnostics

## Runtime options

```csharp
services.AddValiantMappers(options =>
{
    options.Mode = ValiantMappingMode.Auto;
    options.NullHandling = ValiantNullHandling.MapNulls;
});
```

| Option | Values | Default |
| --- | --- | --- |
| `Mode` | `Auto`, `Debuggable`, `Fast` | `Auto` |
| `NullHandling` | `MapNulls`, `IgnoreSourceNulls` | `MapNulls` |

`Auto` uses the debuggable runtime backend in Debug builds and direct generated mapping in other builds. `Debuggable` executes your `Configure` method at runtime, so breakpoints work; this path uses expression compilation and reflection. `Fast` always uses generated direct mapping code and is the preferred deployment path for trimming and Native AOT.

## Generator scope

`[ValiantMapper]` is the discovery boundary. The incremental generator processes only marked declarations and emits:

- the typed `Map` implementation and `IMapper<TSource, TTarget>` contract;
- direct member/constructor mapping for the fast backend;
- mapper registration metadata;
- `AddValiantMappers` when dependency-injection abstractions are present.

A separate narrowly scoped analyzer checks statically knowable `IMapper` and `IMapperFactory` calls for missing pairs without putting ordinary invocations into the generator graph.

## Diagnostics

| ID | Meaning |
| --- | --- |
| `VLM001` | Mapper must be partial |
| `VLM002` | Mapper shape is unsupported |
| `VLM003` | Source/target pair cannot be determined |
| `VLM004` | Mapping declaration syntax is unsupported |
| `VLM005` | Target cannot be constructed |
| `VLM006` | Required target member is not mapped |
| `VLM101` | A target member is not mapped |
| `VLM102` | A source member exists but its type is incompatible |
| `VLM104` | More than one default mapper owns the same pair |
| `VLM105` | `IMapperFactory` call requests an unregistered pair |
| `VLM106` | `IMapper` facade call requests an unregistered pair |

## Constraints

- Mapper and containing declarations that receive generated members must be `partial`.
- Each source/target pair has one default generated mapper.
- Targets need a usable public construction path.
- `Configure` is intentionally generator-readable; unsupported expression shapes produce `VLM004` instead of silently changing behavior.
- Use `TryMap` or `TryCreateMapper` for pairs that are optional at runtime.

