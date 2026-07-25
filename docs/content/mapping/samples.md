---
title: Samples
description: Run the convention, explicit, conditional, null, composition, and dependency-injection mapping samples.
---

# Mapping samples

Select one delegate near the top of
[`Program.cs`](https://github.com/ignitea-dev/valiant/blob/main/mapping/samples/StaticNorth.Valiant.Mapping.Samples/Program.cs),
then run:

```bash
dotnet run --project mapping/samples/StaticNorth.Valiant.Mapping.Samples
```

`Program.cs` is intentionally only a selector. Each folder is self-contained,
and the `Sample` class owns the explanation of the behavior it invokes.

| Delegate | Demonstrates | Source |
| --- | --- | --- |
| `Basic.Sample.Run` | The smallest useful mapper: matching members mapped by convention into an immutable constructor target | [`Basic`](https://github.com/ignitea-dev/valiant/tree/main/mapping/samples/StaticNorth.Valiant.Mapping.Samples/Basic) |
| `ExplicitMapping.Sample.Run` | A custom source transform, a renamed target member, and an intentionally ignored target member | [`ExplicitMapping`](https://github.com/ignitea-dev/valiant/tree/main/mapping/samples/StaticNorth.Valiant.Mapping.Samples/ExplicitMapping) |
| `ConditionalMapping.Sample.Run` | Native `if`/`else`, a chained `When` condition, and the equivalent `Fallback` and `Default` APIs | [`ConditionalMapping`](https://github.com/ignitea-dev/valiant/tree/main/mapping/samples/StaticNorth.Valiant.Mapping.Samples/ConditionalMapping) |
| `NullHandling.Sample.Run` | Mapper-wide null policy, `IgnoreNull`, `MapNull`, `AllowNull`, and a fallback for an ignored null | [`NullHandling`](https://github.com/ignitea-dev/valiant/tree/main/mapping/samples/StaticNorth.Valiant.Mapping.Samples/NullHandling) |
| `FlatteningAndEnums.Sample.Run` | Convention-based flattening, unflattening, and compatible enum conversion | [`FlatteningAndEnums`](https://github.com/ignitea-dev/valiant/tree/main/mapping/samples/StaticNorth.Valiant.Mapping.Samples/FlatteningAndEnums) |
| `Composition.Sample.Run` | Constructor-injected mapper reuse for a nested object and collection elements | [`Composition`](https://github.com/ignitea-dev/valiant/tree/main/mapping/samples/StaticNorth.Valiant.Mapping.Samples/Composition) |
| `DependencyInjection.Sample.Run` | Generated registration; typed mapper, non-generic facade, and factory; successful and missing `Try` APIs | [`DependencyInjection`](https://github.com/ignitea-dev/valiant/tree/main/mapping/samples/StaticNorth.Valiant.Mapping.Samples/DependencyInjection) |

The basic and convention samples show what the generator can infer. Samples
with a `Configure` override demonstrate behavior supported by both the
debuggable runtime backend and the fast generated backend. The dependency
injection sample leaves `ValiantMappingMode.Auto` selected, so Debug builds use
the debuggable backend and Release builds use generated fast mapping.

Continue with [Convention mapping](./guides/conventions.md),
[Explicit mapping](./guides/explicit-mapping.md), or
[Composition and dependency injection](./guides/composition-dependency-injection.md)
for the complete contracts and constraints.
