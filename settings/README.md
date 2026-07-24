# Valiant Settings

Valiant Settings is a source-generation area for strongly typed settings models.

The goal is to generate deterministic settings model schemas that can later be consumed by visualization tools, editors, documentation pages, or other design-time experiences without relying on reflection scanning. The same source-generated metadata can also drive section-aware settings binding helpers.

Install the most specific settings package your application uses. `Valiant.Settings.AspNetCore` forwards `Valiant.Settings` runtime, build, and analyzer assets; `Valiant.Settings.Validation` also forwards the Valiant Validation assets it integrates with. No extra parent package references are required for source generation.

## Quick Start

```csharp
using Valiant.Settings;

[ValiantSettings("ConnectionStrings:Primary")]
public sealed record DbSettings
{
    [ValiantSecret]
    public string? ConnectionString { get; init; }
}
```

The generator uses the marker attribute to emit schema metadata for the settings model. When `SectionPath` is provided, the generated metadata maps the model to that configuration section.
Use `[ValiantSecret]` on sensitive properties so runtime value visualizers can mask them.

## Configuration Binding

When `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Configuration`, and `Microsoft.Extensions.Options.ConfigurationExtensions` are available in the compilation, the source generator also emits dependency injection registration:

```csharp
using Valiant.Settings.DependencyInjection;

builder.Services.AddValiantSettings(builder.Configuration);
```

By default, a section-backed settings model is schema-only. Opt into generated options binding with `configure: true`:

```csharp
[ValiantSettings("ConnectionStrings:Primary", configure: true)]
public sealed record DbSettings
{
    public string? ConnectionString { get; init; }
}
```

For configured settings models, the generated extension binds options from the declared section:

```csharp
services.Configure<DbSettings>(
    configuration.GetSection("ConnectionStrings:Primary"));
```

Schema providers are also registered as `IValiantSettingsSchemaProvider` services so UI and tooling code can enumerate generated settings metadata from DI.

Skip schema-provider registration when an application only wants generated binding and validation:

```csharp
builder.Services.AddValiantSettings(builder.Configuration, options =>
{
    options.RegisterSchemaProviders = false;
});
```

## Validation

Reference `Valiant.Settings.Validation` to opt configured settings models into normal ASP.NET Core options validation with matching Valiant validators. No additional attribute is required on the settings model beyond `[ValiantSettings("Section", configure: true)]`.

```csharp
using Valiant.Settings.DependencyInjection;
using Valiant.Validation.DependencyInjection;

builder.Services.AddValiantValidators();
builder.Services.AddValiantSettings(builder.Configuration, options =>
{
    options.RegisterSchemaProviders = false;
    options.ValidateWithValiant = true;
});
```

When validation is enabled, the generated settings registration adds `IValidateOptions<TSettings>` adapters for settings models that opt into binding. Each adapter looks for a matching `IValidator<TSettings>` from DI. If no validator is registered, validation succeeds by default. Set `RequireValidators = true` to fail when a settings model has no matching validator:

```csharp
builder.Services.AddValiantSettings(builder.Configuration, options =>
{
    options.ValidateWithValiant = true;
    options.RequireValidators = true;
});
```

## ASP.NET Core UI

`Valiant.Settings.AspNetCore` maps a small built-in UI and JSON endpoint for visualizing generated settings schemas:

```csharp
using Valiant.Settings.AspNetCore;
using Valiant.Settings.DependencyInjection;

builder.Services.AddValiantSettings(builder.Configuration);

var app = builder.Build();

app.MapValiantSettingsUi();
```

The default route is `/_valiant/settings`. The UI reads from `/_valiant/settings/schemas`, which returns a JSON document built from registered `IValiantSettingsSchemaProvider` services.

Override the route prefix when mapping the UI:

```csharp
app.MapValiantSettingsUi(options => options.Path = "/admin/settings");
```

The schema endpoint follows the configured prefix, for example `/admin/settings/schemas`.

Opt into runtime values at the UI mapping level:

```csharp
app.MapValiantSettingsUi(options =>
{
    options.ShowRuntimeValues = true;
});
```

Runtime values are read dynamically from `IOptions<TSettings>` for registered schemas only. If `RegisterSchemaProviders = false`, the UI has no generated schema catalog and runtime values are not available. Values marked with `[ValiantSecret]` are masked, and common sensitive property names such as `Password`, `Secret`, `Token`, `ApiKey`, `ClientSecret`, and `ConnectionString` are masked by default.

Allow the UI to request unmasked secret values only when that is acceptable for the mapped endpoint:

```csharp
app.MapValiantSettingsUi(options =>
{
    options.ShowRuntimeValues = true;
    options.AllowSecretReveal = true;
});
```

When `AllowSecretReveal` is enabled, the UI shows a reveal/hide control in the runtime values panel. Secret values are still masked until the user explicitly reveals them.

## Intended Scope

- Discover settings model declarations through an explicit authoring surface.
- Generate schema metadata for model properties, nested objects, collections, default values, and validation hints.
- Preserve configuration section paths such as `ConnectionStrings:Primary` in generated metadata.
- Generate section-aware options registration for settings models that opt into binding.
- Optionally visualize masked runtime values from registered schemas in the ASP.NET Core UI.
- Keep generated schemas deterministic so visualization tools can diff, cache, and render them reliably.
- Preserve the Valiant source-generation goals around trim and Native AOT friendliness.

## Planned Layout

- `src/Valiant.Settings`: runtime APIs and schema metadata contracts.
- `src/Valiant.Settings.AspNetCore`: ASP.NET Core UI and JSON endpoints for generated schemas.
- `src/Valiant.Settings.Validation`: `IValidateOptions<TOptions>` integration for settings models with matching Valiant validators.
- `src/Valiant.Settings.SourceGeneration`: source generator project.
- `tests/Valiant.Settings.Tests`: generator tests and schema-shape tests.
- `samples/Valiant.Settings.Samples`: selectable schema, binding, registry, and registration-options samples.
- `samples/Valiant.Settings.AspNetCore.Samples`: selectable ASP.NET Core UI, route configuration, runtime values, secret reveal, and validation samples.
