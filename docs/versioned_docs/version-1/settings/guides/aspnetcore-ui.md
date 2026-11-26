---
title: ASP.NET Core UI
description: Map the built-in settings explorer, JSON schema endpoint, runtime values, and secret reveal controls.
---

# ASP.NET Core UI

`StaticNorth.Valiant.Settings.AspNetCore` provides a settings explorer and a JSON document backed by generated schema providers.

Treat both routes as administrative endpoints. Configure your application's authentication scheme and an authorization policy named `SettingsAdministrators` before building the app. Apply the policy to the returned route group so it covers both routes.

```csharp
using StaticNorth.Valiant.Settings.AspNetCore;
using StaticNorth.Valiant.Settings.DependencyInjection;

builder.Services.AddValiantSettings(builder.Configuration);

var app = builder.Build();
app.MapValiantSettingsUi()
    .RequireAuthorization("SettingsAdministrators");
```

The default routes are:

| Route | Purpose |
| --- | --- |
| `/_valiant/settings` | Interactive schema explorer |
| `/_valiant/settings/schemas` | Generated schema JSON document |

## Preview schemas during local development

Use this mapping in a local development host to preview the schema without runtime values:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapValiantSettingsUi();
}
```

## Change the route

```csharp
app.MapValiantSettingsUi(options => options.Path = "/admin/settings")
    .RequireAuthorization("SettingsAdministrators");
```

The JSON endpoint follows the prefix at `/admin/settings/schemas`.

## Show current runtime values

```csharp
app.MapValiantSettingsUi(options =>
{
    options.ShowRuntimeValues = true;
}).RequireAuthorization("SettingsAdministrators");
```

Runtime values are resolved dynamically from `IOptions<TSettings>` for registered schemas only. If schema-provider registration is disabled, the UI has no generated catalog to display.

## Protect secrets

`[ValiantSecret]` properties are masked. Valiant also masks common sensitive names such as `Password`, `Secret`, `Token`, `ApiKey`, `ClientSecret`, and `ConnectionString`.

Enable reveal only when every caller authorized by the route policy may read the unmasked values. The same policy governs direct JSON requests and the reveal button.

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

Run the default UI, custom authorization, runtime-value masking, and explicit
secret-reveal variants from the [Settings sample catalog](../samples.md#aspnet-core-settings).
