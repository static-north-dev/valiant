---
title: Binding and validation
description: Generate Options binding and validate configured settings through Valiant validators.
---

# Binding and validation

## Generated Options binding

Set `configure: true` and register settings once:

```csharp
[ValiantSettings("Storage:S3", configure: true)]
public sealed record S3Settings
{
    public required string Bucket { get; init; }
    public string? Region { get; init; }
}

builder.Services.AddValiantSettings(builder.Configuration);
```

Valiant emits the equivalent section-aware `services.Configure<S3Settings>(configuration.GetSection("Storage:S3"))` registration.

Schema providers register by default. Disable them when the application only needs binding and validation:

```csharp
builder.Services.AddValiantSettings(builder.Configuration, options =>
{
    options.RegisterSchemaProviders = false;
});
```

## Validate Options with Valiant

Reference `StaticNorth.Valiant.Settings.Validation` and declare the matching validator:

```csharp
[ValiantValidator]
public sealed partial class S3SettingsValidator
    : ValidatorDefinition<S3Settings>
{
    public override void Configure(ValidationContext<S3Settings> context)
    {
        context.Property(settings => settings.Bucket).Required();
        context.Property(settings => settings.Region).MaxLength(32);
    }
}
```

Register validators and enable the adapter:

```csharp
builder.Services.AddValiantValidators();
builder.Services.AddValiantSettings(builder.Configuration, options =>
{
    options.ValidateWithValiant = true;
});
```

Generated settings registration adds `IValidateOptions<S3Settings>` for configured settings models. It resolves a matching `IValidator<S3Settings>` from DI and translates Valiant errors to normal Options validation failures.

## Await validation at startup on .NET 11

Alongside the Valiant registration above, opt each settings model into startup validation:

```csharp
builder.Services.AddOptions<S3Settings>().ValidateOnStart();
```

The .NET 11 adapter implements `IAsyncValidateOptions<S3Settings>`. The host awaits the Valiant validator's `ValidateAsync` method during startup, passes the startup cancellation token, and reports validation errors in an `OptionsValidationException`.

Read async-only settings from `IOptions<S3Settings>.Value` after startup completes. Configuration reloads and new `IOptionsSnapshot<S3Settings>` scopes create options synchronously and require validators that support synchronous validation.

## Require matching validators

By default, validation succeeds when no validator is registered. Make missing validators a configuration error:

```csharp
builder.Services.AddValiantSettings(builder.Configuration, options =>
{
    options.ValidateWithValiant = true;
    options.RequireValidators = true;
});
```

Only models with `configure: true` receive Options binding and validation adapters. Schema-only models remain metadata contracts.
