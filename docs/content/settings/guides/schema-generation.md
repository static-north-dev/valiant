---
title: Schema generation
description: Describe nested settings, collections, defaults, nullability, configuration keys, and secrets.
---

# Schema generation

`[ValiantSettings]` is the generator discovery boundary. It produces a deterministic `ValiantSettingsSchema` for the marked type.

```csharp
[ValiantSettings("Search")]
public sealed record SearchSettings
{
    public required Uri Endpoint { get; init; }
    public SearchRetrySettings Retry { get; init; } = new();
    public string[] Indexes { get; init; } = [];
}

public sealed record SearchRetrySettings
{
    public int Attempts { get; init; } = 3;
    public TimeSpan Delay { get; init; } = TimeSpan.FromSeconds(1);
}
```

The model schema contains its CLR type, display name, section path, and ordered property schemas. Each property records:

- CLR name and configuration key;
- type display name and normalized kind;
- required and nullable flags;
- secret flag and known default value;
- nested object properties or collection item schema.

Public instance properties with public getters are included, including inherited properties. The most derived declaration wins when a property is overridden or hidden. Overrides retain an inherited `[ValiantSecret]` marker.

## Property kinds

| Kind | Represents |
| --- | --- |
| `String` | Text and string-like configuration values |
| `Boolean` | Boolean values |
| `Integer` | Integral numeric values |
| `Number` | Floating-point and decimal values |
| `Enum` | Enum values |
| `Object` | Nested settings object |
| `Array` | Array or collection with an item schema |
| `Unknown` | A type without a more specific schema classification |

## Schema-only or configured

Schema generation always occurs. Binding is opt-in:

```csharp
[ValiantSettings("Features")]
public sealed record FeatureCatalog; // schema only

[ValiantSettings("Features", configure: true)]
public sealed record BoundFeatureCatalog; // schema + Options binding
```

This lets libraries publish metadata without changing the host's service graph. See [schema lookup without dependency injection](../reference/schema-api.md#schema-lookup-without-dependency-injection). A required flag in the schema describes the model. Use the [Options validation integration](./binding-validation.md#validate-options-with-valiant) to enforce settings at runtime.

## Secret metadata

```csharp
[ValiantSecret]
public string? ClientSecret { get; init; }
```

`ValiantSecret` tells schema consumers that runtime values must be hidden. The ASP.NET Core UI also masks common sensitive names defensively, but explicit attributes are the durable contract for custom tools.

:::info Deterministic output
Generated schemas are stable enough for tooling to sort, diff, cache, and render. No runtime type scan is required to discover marked settings models.
:::
