---
title: Quick start
description: Install StaticNorth.Valiant.Settings, generate a schema, and optionally bind Options.
---

# Quick start

## 1. Create a project and install Valiant

Use the **.NET 10.0.400 SDK or newer** to build this example with Valiant 1.2.1. The project can target .NET 8. Run `dotnet --version` from the project directory to check the selected SDK. A `CS9057` warning indicates that the selected compiler is too old to load the generator.

```bash
dotnet new web -n SettingsDemo -f net8.0
cd SettingsDemo
dotnet add package StaticNorth.Valiant.Settings --version 1.2.1
```

For the [settings explorer](./guides/aspnetcore-ui.md), choose `StaticNorth.Valiant.Settings.AspNetCore`. For [Options validation](./guides/binding-validation.md), choose `StaticNorth.Valiant.Settings.Validation`. Both integrations bring in the core package and its generator.

## 2. Mark a settings model

```csharp title="MailSettings.cs"
using StaticNorth.Valiant.Settings;

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

## 3. Add configuration

Add the `Mail` section to `appsettings.json`:

```json title="appsettings.json"
{
  "Mail": {
    "Host": "smtp.example.com"
  }
}
```

## 4. Register and read the options

Replace the contents of `Program.cs`:

```csharp title="Program.cs"
using Microsoft.Extensions.Options;
using StaticNorth.Valiant.Settings.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddValiantSettings(builder.Configuration);

await using var app = builder.Build();
var settings = app.Services.GetRequiredService<IOptions<MailSettings>>().Value;
Console.WriteLine($"{settings.Host}:{settings.Port}");
```

Run `dotnet run --no-launch-profile`. The output is `smtp.example.com:587`. The generated extension binds the opted-in model and registers its schema provider. In a web application, inject `IOptions<MailSettings>` where you use the settings.

Next: learn how nested models and collections appear in [Schema generation](./guides/schema-generation.md).

