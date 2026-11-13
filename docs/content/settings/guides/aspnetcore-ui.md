---
title: ASP.NET Core UI
description: Map the built-in settings explorer, JSON schema endpoint, runtime values, and secret reveal controls.
---

# ASP.NET Core UI

`StaticNorth.Valiant.Settings.AspNetCore` provides a compact settings explorer and a JSON document backed by generated schema providers.

```csharp
using StaticNorth.Valiant.Settings.AspNetCore;
using StaticNorth.Valiant.Settings.DependencyInjection;

builder.Services.AddValiantSettings(builder.Configuration);

var app = builder.Build();
app.MapValiantSettingsUi();
```

The default routes are:

| Route | Purpose |
| --- | --- |
| `/_valiant/settings` | Interactive schema explorer |
| `/_valiant/settings/schemas` | Generated schema JSON document |

## Change the route

```csharp
app.MapValiantSettingsUi(options =>
    options.Path = "/admin/settings");
```

The JSON endpoint follows the prefix at `/admin/settings/schemas`.

## Show current runtime values

```csharp
app.MapValiantSettingsUi(options =>
{
    options.ShowRuntimeValues = true;
});
```

Runtime values come from the host's default `IOptions<TSettings>` instance for each registered schema. The explorer reports values as unavailable when the host has neither Options configuration for that model nor an explicitly supplied options instance. Runtime values can include defaults and values assigned by configuration or post-configuration. Declared defaults in the property list describe constant initializers.

The explorer loads the schema catalog first and requests values for the selected section when you open its Runtime view. Changing sections clears revealed values. A request to `/schemas` without a section selector still returns the full document.

## Protect secrets

`[ValiantSecret]` properties are masked. Valiant also masks common sensitive names such as `Password`, `Secret`, `Token`, `ApiKey`, `ClientSecret`, and `ConnectionString`.

Explicitly permit reveal only when endpoint authorization and hosting policy make it acceptable:

```csharp
RouteGroupBuilder settings = app.MapValiantSettingsUi(options =>
{
    options.ShowRuntimeValues = true;
    options.AllowSecretReveal = true;
});

settings.RequireAuthorization("SettingsAdministrators");
```

Secret values remain masked until a user selects reveal. The JSON endpoint accepts `?revealSecrets=true` only when `AllowSecretReveal` is enabled.

Masked secret getters are not evaluated. Runtime values are projected through the generated schema: recursive objects beyond the schema boundary become empty objects, and unsupported opaque values become `null`. Options or getter failures return a generic message so exception text cannot expose credentials. Schema responses use `Cache-Control: no-store`, including when secrets are revealed.

Direct callers of `ValiantSettingsSchemaDocumentFactory.Create` must also enable both `revealSecrets` and `canRevealSecrets` to reveal values.

:::danger Treat the explorer as an administrative endpoint
The package does not decide your authorization policy. Protect the returned `RouteGroupBuilder`, especially when runtime values or secret reveal are enabled.
:::

Run the default UI, custom authorization, runtime-value masking, and explicit
secret-reveal variants from the [Settings sample catalog](../samples.md#aspnet-core-settings).
