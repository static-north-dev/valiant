---
title: Settings
description: Generated settings schemas, optional Options binding and validation, and a secure ASP.NET Core explorer.
---

# Settings with a generated contract

Valiant Settings turns strongly typed configuration models into deterministic schemas. Use the same generated metadata for Options binding, validation, documentation, tooling, or the built-in ASP.NET Core settings explorer—without reflection-based assembly scanning.

```csharp
using StaticNorth.Valiant.Settings;

[ValiantSettings("Payments", configure: true)]
public sealed record PaymentSettings
{
    public Uri? Endpoint { get; init; }

    [ValiantSecret]
    public string? ApiKey { get; init; }
}
```

## One declaration, several uses

| Capability | Result |
| --- | --- |
| Schema | Model, property, nesting, collection, default, nullability, required, and secret metadata |
| Options | Generated section-aware `Configure<TSettings>` registration on opt-in models |
| Validation | `IValidateOptions<TSettings>` backed by a generated Valiant validator |
| UI | Built-in schema explorer and JSON endpoint with safe secret masking |
| Tooling | `IValiantSettingsSchemaProvider` for custom consumers |

## Documentation map

- [Quick start](./quick-start.md)
- [Runnable samples](./samples.md)
- [Schema generation](./guides/schema-generation.md)
- [Binding and validation](./guides/binding-validation.md)
- [ASP.NET Core UI](./guides/aspnetcore-ui.md)
- [Schema API, generation, and diagnostics](./reference/schema-api.md)

The [sample catalog](./samples.md) covers every selectable core and ASP.NET
Core scenario for schema metadata, binding, validation, and the safe-by-default
UI.
