---
title: Quick start
description: Install Valiant.Settings, generate a schema, and optionally bind Options.
---

# Quick start

## 1. Choose a package

```bash
# Schema generation and optional Options binding
dotnet add package Valiant.Settings

# Or install a specific integration
dotnet add package Valiant.Settings.AspNetCore
dotnet add package Valiant.Settings.Validation
```

Specific integration packages forward the core runtime, build, and generator assets they depend on. Add multiple integration packages only when the application uses multiple integrations.

## 2. Mark a settings model

```csharp
using Valiant.Settings;

[ValiantSettings("Mail", configure: true)]
public sealed record MailSettings
{
    public required string Host { get; init; }
    public int Port { get; init; } = 587;

    [ValiantSecret]
    public string? Password { get; init; }
}
```

The section path is required. `configure: true` opts this model into generated Options binding; omit it for schema-only metadata.

## 3. Register generated settings

```csharp
using Valiant.Settings.DependencyInjection;

builder.Services.AddValiantSettings(builder.Configuration);
```

The generated extension binds opted-in models and registers every generated schema provider.

## 4. Consume the options

```csharp
public sealed class MailSender(IOptions<MailSettings> options)
{
    private readonly MailSettings _settings = options.Value;
}
```

Or enumerate schemas:

```csharp
IEnumerable<IValiantSettingsSchemaProvider> schemas =
    provider.GetServices<IValiantSettingsSchemaProvider>();
```

Next: learn how nested models and collections appear in [Schema generation](./guides/schema-generation.md).

