---
title: Mapping
description: Compile-time object mapping with direct generated code, explicit rules, composition, and DI.
slug: /
---

# Mapping that stays visible

Valiant Mapping turns a small mapper declaration into direct C# assignments. It handles conventional members, immutable targets, nested shapes, collections, enums, explicit transformations, conditional rules, and mapper composition—without runtime assembly scanning.

```csharp
using Valiant.Mapping;

[ValiantMapper]
public sealed partial class UserMapper : MapperDefinition<User, UserDto>;

var dto = new UserMapper().Map(user);
```

The generated partial class implements `IMapper<User, UserDto>` and exposes a strongly typed `Map(User source)` method.

## Why use it?

| Capability | What Valiant generates |
| --- | --- |
| Convention mapping | Direct reads and assignments for compatible public fields and properties |
| Immutable targets | Constructor arguments for records and constructor-only models |
| Shape changes | Flattening, unflattening, enum conversion, and reusable nested/collection mappers |
| Explicit ownership | `Map`, `Ignore`, conditions, fallbacks, and null policy in `Configure` |
| Runtime selection | Typed mapper, `IMapper` facade, and `IMapperFactory` through generated DI registration |
| Deployment | A fast generated backend suited to trimming and Native AOT, plus a debuggable backend |

:::tip Start with conventions
Declare the mapper and compile. Add `Configure` only when the source and target shapes intentionally differ. Once `Configure` is present, list every target mapping you own explicitly.
:::

## Documentation map

- [Quick start](./quick-start.md) — install, declare, register, and call a mapper.
- [Runnable samples](./samples.md) — select and run every maintained Mapping scenario.
- [Convention mapping](./guides/conventions.md) — records, constructors, flattening, collections, and enums.
- [Explicit mapping](./guides/explicit-mapping.md) — transformations, conditions, ignores, fallbacks, and nulls.
- [Composition and DI](./guides/composition-dependency-injection.md) — nested mappers, facade, and factory.
- [Runtime and diagnostics](./reference.md) — modes, generated scope, warnings, and limitations.

The [sample catalog](./samples.md) covers every executable Mapping scenario and
links each one to its self-contained source folder.
