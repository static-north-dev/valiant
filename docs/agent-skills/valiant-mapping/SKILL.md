---
name: valiant-mapping
description: Author StaticNorth.Valiant object mappings, including conventions, explicit rules, immutable targets, composition, and generated dependency injection.
---

# Valiant Mapping

Check the application's package major and select its version in [the documentation index](https://valiant.staticnorth.dev/agents/documents.json). The default is the latest published major. A preview may also list `current`. Read the corresponding quick start and feature guide before changing mapper APIs.

Latest published references: [Quick start](https://valiant.staticnorth.dev/mapping/quick-start), [Explicit mapping](https://valiant.staticnorth.dev/mapping/guides/explicit-mapping), and [Composition and dependency injection](https://valiant.staticnorth.dev/mapping/guides/composition-dependency-injection).

- Install `StaticNorth.Valiant.Mapping`; it supplies the runtime and analyzer together.
- Mark a `partial` mapper with `[ValiantMapper]` and derive from `MapperDefinition<TSource, TTarget>`. The generator discovers marked declarations, not every mapping-shaped type in the compilation.
- Start with convention mapping. Add explicit rules only for the mismatches or behavior the task requires, following the matching version's API.
- Use the generated concrete mapper or `IMapper<TSource, TTarget>` for a known pair. Use documented factory/facade APIs for dynamic pair selection.
- Register generated services with `AddValiantMappers()` from `StaticNorth.Valiant.Mapping` when DI is used. Do not replace generated registration with reflection scanning.
- Preserve the intended null-handling policy and target construction semantics, especially for records, immutable targets, and nested mappings.
- When composing mappers, preserve dependency ownership and existing registration lifetimes.

For `Configure`-based mappings, test independent source and mapper instances in Fast and Debuggable modes. Assert the expected target independently in each mode before comparing complete snapshots; agreement alone does not prove the intended mapping.
