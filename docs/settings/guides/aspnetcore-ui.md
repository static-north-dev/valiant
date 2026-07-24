---
title: ASP.NET Core UI
description: Map the built-in settings explorer, JSON schema endpoint, runtime values, and secret reveal controls.
---

# ASP.NET Core UI

`Valiant.Settings.AspNetCore` provides a compact settings explorer and a JSON document backed by generated schema providers.

```csharp
using Valiant.Settings.AspNetCore;
using Valiant.Settings.DependencyInjection;

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

Runtime values are resolved dynamically from `IOptions<TSettings>` for registered schemas only. If schema-provider registration is disabled, the UI has no generated catalog to display.

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

:::danger Treat the explorer as an administrative endpoint
The package does not decide your authorization policy. Protect the returned `RouteGroupBuilder`, especially when runtime values or secret reveal are enabled.
:::

Run the default UI, custom authorization, runtime-value masking, and explicit
secret-reveal variants from the [Settings sample catalog](../samples.md#aspnet-core-settings).
