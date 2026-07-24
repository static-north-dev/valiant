---
title: Samples
description: Run the generated schema, Options binding, validation, and ASP.NET Core settings explorer samples.
---

# Settings samples

Settings has separate console and ASP.NET Core sample projects. In each project,
select one delegate or startup near the top of `Program.cs`.

Settings source generation is assembly-wide. Selecting one scenario changes
which code runs, but every `[ValiantSettings]` model in that project contributes
to `ValiantSettingsSchemas` and the generated `AddValiantSettings` method. The
samples therefore use distinct section paths.

## Core settings

Run the console project:

```bash
dotnet run --project settings/samples/Valiant.Settings.Samples
```

Select one delegate in
[`Program.cs`](https://github.com/ignitea-dev/valiant/blob/main/settings/samples/Valiant.Settings.Samples/Program.cs):

| Delegate | Demonstrates | Source |
| --- | --- | --- |
| `Basic.Sample.Run` | The normal configured-settings flow: `[ValiantSettings(..., configure: true)]`, `AddValiantSettings(configuration)`, and `IOptions<TSettings>` consumption | [`Basic`](https://github.com/ignitea-dev/valiant/tree/main/settings/samples/Valiant.Settings.Samples/Basic) |
| `RegistrationOptions.Sample.Run` | Default schema-provider registration compared with `RegisterSchemaProviders = false`, while configured Options still bind | [`RegistrationOptions`](https://github.com/ignitea-dev/valiant/tree/main/settings/samples/Valiant.Settings.Samples/RegistrationOptions) |
| `SchemaRegistry.Sample.Run` | `ValiantSettingsSchemas.All`, `Get<TSettings>`, and successful and unsuccessful `TryGet<TSettings>` lookups | [`SchemaRegistry`](https://github.com/ignitea-dev/valiant/tree/main/settings/samples/Valiant.Settings.Samples/SchemaRegistry) |
| `SchemaMetadata.Sample.Run` | Property kinds, nested objects, collections, nullability, required members, and secret metadata | [`SchemaMetadata`](https://github.com/ignitea-dev/valiant/tree/main/settings/samples/Valiant.Settings.Samples/SchemaMetadata) |

`SchemaMetadata` also shows that a runtime property initializer is not emitted
as schema `DefaultValue` metadata.

## ASP.NET Core settings

Run the web project:

```bash
dotnet run --project settings/samples/Valiant.Settings.AspNetCore.Samples
```

Select one startup type in
[`Program.cs`](https://github.com/ignitea-dev/valiant/blob/main/settings/samples/Valiant.Settings.AspNetCore.Samples/Program.cs):

| Startup | Demonstrates | Source |
| --- | --- | --- |
| `Basic.Startup` | `AddValiantSettings()` and the default `/_valiant/settings` explorer and schema routes | [`Basic`](https://github.com/ignitea-dev/valiant/tree/main/settings/samples/Valiant.Settings.AspNetCore.Samples/Basic) |
| `Configuration.Startup` | A custom `/admin/settings` path and authorization conventions on the returned `RouteGroupBuilder` | [`Configuration`](https://github.com/ignitea-dev/valiant/tree/main/settings/samples/Valiant.Settings.AspNetCore.Samples/Configuration) |
| `RuntimeValues.Startup` | `ShowRuntimeValues = true`, explicit secret masking, defensive name-based masking, and reveal disabled | [`RuntimeValues`](https://github.com/ignitea-dev/valiant/tree/main/settings/samples/Valiant.Settings.AspNetCore.Samples/RuntimeValues) |
| `SecretReveal.Startup` | `AllowSecretReveal = true` with explicit `?revealSecrets=true` opt-in | [`SecretReveal`](https://github.com/ignitea-dev/valiant/tree/main/settings/samples/Valiant.Settings.AspNetCore.Samples/SecretReveal) |
| `Validation.Startup` | `ValidateWithValiant = true` with permissive behavior when a configured model has no validator | [`Validation`](https://github.com/ignitea-dev/valiant/tree/main/settings/samples/Valiant.Settings.AspNetCore.Samples/Validation) |
| `Validation.InvalidStartup` | Valiant failures translated into `OptionsValidationException` failures | [`Validation`](https://github.com/ignitea-dev/valiant/tree/main/settings/samples/Valiant.Settings.AspNetCore.Samples/Validation) |
| `Validation.RequiredStartup` | `RequireValidators = true` failing when a configured settings model has no matching validator | [`Validation`](https://github.com/ignitea-dev/valiant/tree/main/settings/samples/Valiant.Settings.AspNetCore.Samples/Validation) |

The three validation startups intentionally share one folder because they use
the same settings models and validators while exercising different registration
policies.

## Try the web scenarios

With the HTTP launch profile running on port `5194`:

```bash
curl -i http://localhost:5194/_valiant/settings
curl -i -H "X-Settings-Admin: true" http://localhost:5194/admin/settings
curl -s http://localhost:5194/runtime-values/schemas
curl -s "http://localhost:5194/secret-reveal/schemas?revealSecrets=true"
curl -i http://localhost:5194/validation/invalid
```

The configuration sample uses a deliberately small header-based demo
authentication handler. Replace it with the host application's real
authentication and authorization policy.

Runtime values and secret reveal are administrative capabilities. Protect the
returned route group before enabling either outside local development. See
[ASP.NET Core UI](./guides/aspnetcore-ui.md) and
[Binding and validation](./guides/binding-validation.md) for the production
configuration.
