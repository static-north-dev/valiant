---
title: Schema API, generation, and diagnostics
description: Consume generated settings metadata programmatically and understand package flow and generator constraints.
---

# Schema API, generation, and diagnostics

## Provider contract

Each generated provider implements:

```csharp
public interface IValiantSettingsSchemaProvider
{
    Type SettingsType { get; }
    string? SectionPath { get; }
    ValiantSettingsSchema Schema { get; }
}
```

Resolve all providers to build custom documentation, editors, configuration audits, or deployment tooling:

```csharp
foreach (var provider in services.GetServices<IValiantSettingsSchemaProvider>())
{
    Console.WriteLine($"{provider.Schema.Name}: {provider.SectionPath}");

    foreach (var property in provider.Schema.Properties)
    {
        Console.WriteLine($"  {property.ConfigurationKey} ({property.Kind})");
    }
}
```

## Model schema

`ValiantSettingsSchema` exposes `SettingsType`, `Name`, `SectionPath`, and `Properties`.

`ValiantSettingsPropertySchema` exposes:

- `Name`, `ConfigurationKey`, and `TypeName`;
- `Kind`, `IsRequired`, `IsNullable`, and `IsSecret`;
- `DefaultValue` when it is known at generation time;
- `Item` for arrays/collections;
- `Properties` for nested objects.

## ASP.NET Core document

The UI JSON endpoint returns `ValiantSettingsSchemaDocument`. It contains ordered `ValiantSettingsSchemaEntry` values and indicates whether secrets can be revealed and whether the current response contains revealed values. Each entry can include masked runtime values or a runtime-value resolution error.

## Packages

| Package | Use it for | Target |
| --- | --- | --- |
| `StaticNorth.Valiant.Settings` | Schema generation and optional Options binding | .NET 8 and .NET Standard 2.0 |
| `StaticNorth.Valiant.Settings.AspNetCore` | Built-in explorer and JSON schema endpoint | .NET 8 |
| `StaticNorth.Valiant.Settings.Validation` | Options validation through Valiant validators | .NET 8 |

Integration packages forward their parent runtime and generator assets. `StaticNorth.Valiant.Settings.Validation` also carries the core Valiant Validation assets it integrates with.

## Generator scope and diagnostics

Only types marked with `[ValiantSettings]` enter the incremental generator pipeline. Generated output includes the schema provider and, when the required Microsoft configuration/DI APIs are referenced, the aggregated `AddValiantSettings` registration surface.

| ID | Meaning |
| --- | --- |
| `VLS001` | Generic settings model types are not supported |
| `VLS002` | The settings section path is empty |

The section path is deliberately required so generated binding and external tooling share one unambiguous configuration identity.

