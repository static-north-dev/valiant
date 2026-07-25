# Settings samples

The settings area uses the same sample convention as endpoints:

- each feature has one top-level folder;
- files live directly in that folder instead of `UseCases/Models/Scenarios`;
- the runnable `Program.cs` selects one sample or `Startup` near the top;
- project names use the plural `Samples` suffix.

Settings source generation is assembly-wide. Selecting one sample changes which code runs,
but every `[ValiantSettings]` model in that project contributes to
`ValiantSettingsSchemas` and the generated `AddValiantSettings` registration method.
Every sample therefore uses a unique section path.

## Core settings

```bash
dotnet run --project settings/samples/StaticNorth.Valiant.Settings.Samples
```

Select the active delegate near the top of
`StaticNorth.Valiant.Settings.Samples/Program.cs`.

| Folder | Demonstrates |
| --- | --- |
| `Basic` | the normal happy path: mark a configured model, call `AddValiantSettings(configuration)`, and consume `IOptions<T>` |
| `RegistrationOptions` | default DI provider registration and `RegisterSchemaProviders = false` while configured Options still bind |
| `SchemaRegistry` | `ValiantSettingsSchemas.All`, `Get`, and positive/negative `TryGet` lookups |
| `SchemaMetadata` | all property kinds, required/nullability flags, nested objects, scalar/object arrays, and secret metadata |

`SchemaMetadata` also makes the current initializer limitation explicit: runtime property
initializers are not emitted as schema `DefaultValue` metadata.

## ASP.NET Core settings

```bash
dotnet run --project settings/samples/StaticNorth.Valiant.Settings.AspNetCore.Samples
```

Select the active `Startup` near the top of
`StaticNorth.Valiant.Settings.AspNetCore.Samples/Program.cs`.

| Folder/startup | Demonstrates |
| --- | --- |
| `Basic.Startup` | default `/_valiant/settings` UI and schema routes |
| `Configuration.Startup` | custom `/admin/settings` path and conventions on the returned `RouteGroupBuilder` |
| `RuntimeValues.Startup` | `ShowRuntimeValues = true`, explicit secret masking, name-based defensive masking, and reveal disabled |
| `SecretReveal.Startup` | `AllowSecretReveal = true` with explicit `?revealSecrets=true` opt-in |
| `Validation.Startup` | `ValidateWithValiant = true` and the permissive missing-validator default |
| `Validation.InvalidStartup` | Valiant failures translated to `OptionsValidationException` failures |
| `Validation.RequiredStartup` | `RequireValidators = true` failure when a validator is missing |

The configuration sample protects its route group with a deliberately small demo policy:
send `X-Settings-Admin: true` to access it. Replace that assertion with the host
application's real authentication and authorization policy.

With the HTTP launch profile running on port `5194`:

```bash
curl -i http://localhost:5194/_valiant/settings
curl -i -H "X-Settings-Admin: true" http://localhost:5194/admin/settings
curl -s http://localhost:5194/runtime-values/schemas
curl -s "http://localhost:5194/secret-reveal/schemas?revealSecrets=true"
curl -i http://localhost:5194/validation/invalid
```

Runtime values and secret reveal are administrative capabilities. Protect the returned
route group with the host's authorization policy before enabling either outside local
development.
