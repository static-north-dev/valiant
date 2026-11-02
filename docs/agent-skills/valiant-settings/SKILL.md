---
name: valiant-settings
description: Generate StaticNorth.Valiant settings schemas and Options binding, integrate validation, and expose the ASP.NET Core settings explorer with deliberate secret handling.
---

# Valiant Settings

Identify the application's package major and choose the matching version from [the documentation index](https://valiant.staticnorth.dev/agents/documents.json). The default is the latest published major; `current` is development documentation.

Latest published references: [Quick start](https://valiant.staticnorth.dev/settings/quick-start), [Binding and validation](https://valiant.staticnorth.dev/settings/guides/binding-validation), and [ASP.NET Core UI](https://valiant.staticnorth.dev/settings/guides/aspnetcore-ui).

- Install `StaticNorth.Valiant.Settings` for schemas and optional Options binding. Choose `.AspNetCore` or `.Validation` when that integration is needed; they carry their parent runtime and generator assets.
- `[ValiantSettings("Section:Path")]` generates schema metadata. Add `configure: true` only when the model should also receive generated configuration binding.
- Register generated settings with `AddValiantSettings(configuration)` from `StaticNorth.Valiant.Settings.DependencyInjection`.
- Mark secret properties with `[ValiantSecret]`. The ASP.NET Core schema consumer masks secret runtime values server-side by default.
- `RegisterSchemaProviders = false` preserves generated Options binding but prevents schema discovery by schema consumers. Do not expect the settings explorer to rediscover unregistered models.
- The validation bridge requires matching generated settings and `IValidator<TSettings>` services. Verify invalid configuration through the actual Options validation path.
- Runtime value display and secret reveal are separate opt-ins. Enable only the behavior the user requested, and preserve the host application's endpoint authorization.

Test schema generation, binding, and runtime visualization as distinct behaviors. For explorer changes, cover the registered-model boundary and verify masked responses directly, rather than relying only on what the browser hides.
