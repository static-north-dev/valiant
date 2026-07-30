# Valiant Repository Guide

This file is the working guide for coding agents. It summarizes project structure, workflows, and local conventions so a new session can get productive quickly.

## Project Summary

Valiant is an experimental .NET validation library. Users author validators as partial classes marked with `[ValiantValidator]` and either deriving from `ValidatorDefinition<T>` for attribute-based validation or implementing `IValidatorDefinition<T>` for fluent rules. The source generator emits fast `IValidator<T>` implementations, validation metadata providers, and a DI extension when `Microsoft.Extensions.DependencyInjection` is available.

Design goals:
- Small property-focused validation API.
- Source-generated validation without reflection scanning.
- Trim and Native AOT friendliness.
- DI registration generated at compile time.
- Debuggable fallback mode that executes user-authored `Configure` methods.

## Repository Layout

- `StaticNorth.Valiant.sln`: solution entry point.
- `validation/src/StaticNorth.Valiant.Validation`: core runtime package and pack target for the source generator analyzer.
- `validation/src/StaticNorth.Valiant.Validation.SourceGeneration`: incremental source generator.
- `validation/src/StaticNorth.Valiant.Validation.AspNetCore`: MVC and Minimal API integration.
- `validation/src/StaticNorth.Valiant.Validation.AspNetCore.OpenApi`: OpenAPI metadata integration.
- `validation/src/StaticNorth.Valiant.Validation.Mediator`: Mediator integration.
- `validation/src/StaticNorth.Valiant.Validation.MediatR`: MediatR integration.
- `settings/src/StaticNorth.Valiant.Settings`: runtime APIs and schema metadata contracts for settings source generation.
- `settings/src/StaticNorth.Valiant.Settings.AspNetCore`: ASP.NET Core UI and JSON endpoints for visualizing generated settings schemas.
- `settings/src/StaticNorth.Valiant.Settings.Validation`: bridge package that adapts Valiant validators to `IValidateOptions<TOptions>` for settings models.
- `settings/src/StaticNorth.Valiant.Settings.SourceGeneration`: incremental source generator for settings schema metadata.
- `settings/tests/StaticNorth.Valiant.Settings.Tests`: settings source generator tests.
- `settings/samples/StaticNorth.Valiant.Settings.Samples`: selectable settings schema, binding, and registration samples.
- `settings/samples/StaticNorth.Valiant.Settings.AspNetCore.Samples`: selectable ASP.NET Core UI, runtime values, secrets, and validation samples.
- `validation/tests/StaticNorth.Valiant.Validation.Tests`: source generator, fluent rule, ASP.NET Core, OpenAPI, and mediator tests.
- `validation/samples/StaticNorth.Valiant.Validation.Samples`: selectable console/core validation samples.
- `validation/samples/StaticNorth.Valiant.Validation.AspNetCore.Samples`: selectable ASP.NET Core validation samples.
- `mapping/samples/StaticNorth.Valiant.Mapping.Samples`: selectable mapping samples for conventions, explicit rules, conditions, null handling, composition, and generated DI.
- `assets/logo.png`: NuGet package icon, expected to be 128x128 PNG.

## Common Commands

Use these from the repository root:

```bash
dotnet build StaticNorth.Valiant.sln
dotnet test --solution StaticNorth.Valiant.sln
```

The local `dotnet` in this environment expects solution test runs as `dotnet test --solution StaticNorth.Valiant.sln`; `dotnet test StaticNorth.Valiant.sln` fails.

For package validation, use the serial package orchestrator. In this environment, `dotnet pack StaticNorth.Valiant.sln --no-build` has hung silently before:

```bash
dotnet msbuild StaticNorth.Valiant.Pack.proj -t:VerifyPackageConsumers -m:1 -p:Configuration=Release -p:NoBuild=true
```

If a `dotnet pack` command hangs, stop it before starting another pack command.

Dependency versions are centralized in `Directory.Packages.props`. Solution
projects commit lock files and CI restores them with `--locked-mode`; regenerate
them with `dotnet restore StaticNorth.Valiant.sln --force-evaluate` after dependency changes.
Package-consumer smoke projects intentionally stay outside `StaticNorth.Valiant.sln` and
restore produced packages through `smoke/NuGet.Config`.

The SDK version in `global.json` is also an exact dependency:
`rollForward: disable` is intentional. AOT-compatible projects receive
SDK-provided packages such as `Microsoft.NET.ILLink.Tasks`, so rolling to a
different SDK servicing patch can invalidate committed lock files. Upgrade the
SDK deliberately by changing `global.json`, running
`dotnet restore StaticNorth.Valiant.sln --force-evaluate`, reviewing every lock-file change
(especially the ILLink package), and rerunning build, tests, package consumers,
and Native AOT verification.

Integration package rule: when an integration package is the intended single installation point for a parent source-generator package, set its parent runtime `ProjectReference` to `PrivateAssets="contentfiles"`. The SDK default also makes `build` and `analyzers` private, which would otherwise prevent the parent's generator and build-transitive assets from reaching consumers. Keep content files private so integration-only shims do not flow unexpectedly.

## Public Authoring Model

Typical validator:

```csharp
using StaticNorth.Valiant.Validation;

[ValiantValidator]
public sealed partial class CreateUserValidator : ValidatorDefinition<CreateUser>;
```

Fluent rule validator:

```csharp
using StaticNorth.Valiant.Validation;

[ValiantValidator]
public sealed partial class CreateUserFluentValidator : ValidatorDefinition<CreateUser>
{
    public override void Configure(ValidationContext<CreateUser> context)
    {
        context.Property(user => user.Name).Required().MaxLength(64);
    }
}
```

Important contracts:
- `ValiantValidatorAttribute` is the generator discovery hook.
- `ValidatorDefinition<T>` is the recommended authoring surface for both fluent and attribute-based validation. Override `Configure` for fluent rules; omit it for model attributes.
- `IValidatorDefinition<T>` remains the underlying compatibility contract, but public examples and samples should inherit `ValidatorDefinition<T>`.
- The generator emits `IValidator<T>` and, for each default validator model in the current
  compilation, a hidden typed invocation bridge registered by the generated `AddValiantValidators()`.
- Parameterless Minimal API validation must prefer generated invocation bridges so the normal
  `[ValiantValidator]` plus `AddValiantValidators()` path remains reflection-free and Native AOT-safe.
  Raw manual `IValidator<T>` registrations are a JIT compatibility path; the typed endpoint overload
  is the explicit manual Native AOT path.
- Recommended examples use the instance `Configure` override, which naturally supports constructor-injected validator dependencies.
- `ValidatorDefinition<T>` authoring is mutually exclusive: omit `Configure` to use validation attributes, or declare `Configure` for fluent rules on models without validation attributes. Mixing both reports an analyzer error.
- Do not change `IValidatorDefinition<T>` to `static abstract` without deliberately dropping `netstandard2.0` and instance `Configure` support.

Whole-model reuse uses `ValidationContext<T>.UseValidator(IValidator<T>)`.

Property-value reuse uses `Property(...).UseValidator(IPropertyValidator<TProperty>)`. Property validators are separate from model validators and are authored by inheriting `PropertyValidatorDefinition<TProperty>`. They are discovered for DI when they are marked with `[ValiantValidator]`; `PropertyValidatorDefinition<TProperty>` implements the runtime `IPropertyValidator<TProperty>` contract and lets authors override sync, async, or both methods.

## Source Generator Architecture

Main files:
- `ValiantGenerator.cs`: incremental pipeline and candidate creation.
- `ValiantGenerator.Models.cs`: internal generator models.
- `ValiantGenerator.Syntax.cs`: syntax helpers and rewriting.
- `ValiantGenerator.RuleParsing.cs`: property rule parsing.
- `ValiantGenerator.BlockParsing.cs`: native block, conditional, switch, and loop parsing.
- `ValiantGenerator.Diagnostics.cs`: diagnostics and stable model fingerprints.
- `ValiantGenerator.Emission.cs`: generated source emission.

Architecture rules:
- Keep generator discovery narrow. Use `ForAttributeWithMetadataName` for `[ValiantValidator]` and `[ValiantValidator<T>]`.
- Do not reintroduce a broad `CompilationProvider.Combine(validators)` for validator emission.
- Per-validator source output should stay per candidate. DI output should consume only lightweight registration models.
- Treat full `CompilationProvider` data as high invalidation cost. If needed, project it into a small stable value, such as a boolean.
- Avoid carrying Roslyn symbols into emission when stable strings are enough.
- If adding model fields that affect generated output or diagnostics, update the validator fingerprint in `ValiantGenerator.Diagnostics.cs`.
- If adding generated DI registration shape, update `DependencyInjectionValidatorModel` instead of passing full `ValidatorModel` into DI emission.

Current incremental shape:
- Non-generic and generic validator attributes are separate syntax providers.
- Each provider emits validator source independently.
- Valid validators are projected into `DependencyInjectionValidatorModel` for DI output.
- `ValidatorModelComparer` compares stable fingerprints to reduce unchanged output churn.

Cross-generator performance rules:
- Validation, mapping, settings, and endpoint generation start from `ForAttributeWithMetadataName`; do not add compilation-wide type/interface discovery to a generator pipeline.
- Endpoint missing-marker diagnostics live in `ValiantEndpointDeclarationAnalyzer` and intentionally cover direct `IEndpoint` / `IEndpointGroup` base-list declarations only. Aliased and indirect interface implementations are not scanned.
- Mapper factory/facade call diagnostics live in `ValiantMapperInvocationAnalyzer`; local mapper pairs are discovered independently from attributed declarations, while referenced pairs use assembly registration metadata.
- Incremental output models use value-only `ValiantDiagnosticLocation` data. Roslyn symbols, syntax nodes, semantic models, compilations, and `Location` objects must not be reachable from registered provider outputs.
- Important per-item and aggregate providers have tracking names. Incrementality tests should reuse the same driver and assert `Cached` or `Unchanged` after unrelated edits.
- Generated dynamic lookup remains allocation-free for small registration sets. Mapping and settings switch to generated dictionaries only above the internal threshold where linear dispatch becomes less attractive.
- Reuse source-generator primitives from `shared/src/StaticNorth.Valiant.SourceGeneration`: diagnostics, type-pair identity, symbol-derived hint names, generated-file headers, literals, declaration syntax, and feature detection belong there when their behavior is package-neutral. Preserve existing generated hint-name formats when moving logic into a shared helper.
- `shared/src/StaticNorth.Valiant.SourceGeneration/*.cs` is compiled into every generator through a wildcard; it must contain only analyzer-safe production helpers. Cross-generator test helpers belong under `shared/tests/` and are linked explicitly from each test project.
- Generator tests share Roslyn compilation, trusted-platform-reference, execution, and tracked-step mechanics through `shared/tests/StaticNorth.Valiant.SourceGeneration.Testing`. Keep generator-specific source setup, references, analyzers, diagnostic filtering, and compile/load assertions in the owning test project.

## Source Generator Behavior To Preserve

The generator supports:
- Marker attribute `[ValiantValidator]` with `IValidatorDefinition<T>`.
- Marker attribute `[ValiantValidator]` with `ValidatorDefinition<T>` for attribute-based validation.
- Compatibility forms `[ValiantValidator(typeof(T))]` and `[ValiantValidator<T>]`.
- Nested validators only when containing types are partial.
- Public `Configure` with exactly one `ValidationContext<T>` parameter.
- Static and instance `Configure` methods.
- Constructor chaining for validators with public constructors plus generated `ValiantValidationOptions` constructor overloads.
- Debuggable runtime fallback when validators declare `Configure`.
- Generated DI extension `AddValiantValidators` when DI symbols exist in the compilation.

Generated DI registers:
- Concrete validator type.
- `IValidator<TModel>`.
- `IValiantValidationMetadataProvider`.
- `ValiantValidationOptions` singleton.

Settings generator note:
- `[ValiantSettings("Section:Path")]` emits schema metadata with the section path.
- `[ValiantSettings("Section:Path", configure: true)]` also opts the model into generated options binding with `configuration.GetSection(...)`.
- `[ValiantSecret]` marks settings properties whose runtime values must be hidden in schema consumers such as the ASP.NET Core UI.
- When Microsoft.Extensions DI/configuration/options symbols are available, the generator emits `AddValiantSettings(...)`, registers schema providers, and configures settings models that opt into binding.
- Generated `AddValiantSettings(...)` overloads accept `ValiantSettingsOptions`; set `RegisterSchemaProviders = false` to skip registering schema providers while preserving generated options binding.
- When `StaticNorth.Valiant.Settings.Validation` is referenced, the settings generator also emits an `AddValiantSettings(..., Action<ValiantSettingsValidationOptions>?)` overload that can register `IValidateOptions<TSettings>` adapters for configured settings models backed by matching `IValidator<TSettings>` services.
- `MapValiantSettingsUi(options => options.ShowRuntimeValues = true)` enriches the schema endpoint with runtime values read from `IOptions<TSettings>` for registered schemas only; it does not discover settings when schema providers were not registered.
- `MapValiantSettingsUi(options => options.AllowSecretReveal = true)` allows the UI to request unmasked secret runtime values; without it, secrets remain masked server-side.

## Validation Surface

Core concepts:
- `ValidationContext<T>` creates property validations and defers runtime validations.
- `PropertyValidation<T, TProperty>` hosts common checks and `UseValidator`.
- Typed extension files add type-constrained checks.
- `ValidationResult` separates `Failures`, `Errors`, `Warnings`, and `Infos`.
- `ValiantFailureBehavior` controls fail-fast versus validate-all behavior.
- `ValiantValidationMode` controls generated fast mode versus debuggable runtime mode.

Supported checks are documented in `README.md`; keep the README in sync when adding new public checks.

## Test Guidance

Primary test command:

```bash
dotnet test --solution StaticNorth.Valiant.sln
```

Source generator tests are in `validation/tests/StaticNorth.Valiant.Validation.Tests/SourceGenerationTests.cs`.

When changing generator output:
- Add or update focused generator tests.
- For incremental/performance-sensitive changes, add tests that compare generated source stability across unrelated edits.
- Run the full test suite after generator changes.

When changing fluent runtime behavior:
- Update `FluentRuleTests.cs`.
- Add or update `ValidationModeParityTests.cs` for every rule family or composition path whose generated and runtime implementations can diverge.
- Create separate validators and model instances for fast and debuggable executions, assert the intended semantics independently, and compare the complete ordered `Failures` sequence (property, code, message, and severity) plus the `Errors`, `Warnings`, and `Infos` partitions.
- Cover synchronous and asynchronous APIs, failure behavior, cancellation, and `ValiantValidationMode.Auto` when the changed path affects them.
- Run the validation tests in both Debug and Release. `Auto` selects Debuggable in Debug builds and Fast in Release builds:

```bash
dotnet test --project validation/tests/StaticNorth.Valiant.Validation.Tests/StaticNorth.Valiant.Validation.Tests.csproj --configuration Debug
dotnet test --project validation/tests/StaticNorth.Valiant.Validation.Tests/StaticNorth.Valiant.Validation.Tests.csproj --configuration Release
```

When changing Mapping samples or mapper behavior:
- Keep the selectable samples under `mapping/samples/StaticNorth.Valiant.Mapping.Samples`; `Program.cs` should only select a sample, while each `Sample` class owns its description.
- Mirror every demonstrated behavior in `MappingSampleTests.cs`.
- Add or update `MappingModeParityTests.cs` for every `Configure`-based path, create independent mapper/source instances, assert the expected target independently in each mode, and then compare complete target snapshots.
- Add focused generated-source assertions when dispatch shape changes.
- Run the Mapping tests in both Debug and Release. `Auto` selects Debuggable in Debug builds and Fast in Release builds:

```bash
dotnet test --project mapping/tests/StaticNorth.Valiant.Mapping.Tests/StaticNorth.Valiant.Mapping.Tests.csproj --configuration Debug
dotnet test --project mapping/tests/StaticNorth.Valiant.Mapping.Tests/StaticNorth.Valiant.Mapping.Tests.csproj --configuration Release
```

## Packaging Notes

Packable projects:
- `StaticNorth.Valiant.Validation`
- `StaticNorth.Valiant.Validation.AspNetCore`
- `StaticNorth.Valiant.Validation.AspNetCore.OpenApi`
- `StaticNorth.Valiant.Validation.Mediator`
- `StaticNorth.Valiant.Validation.MediatR`
- `StaticNorth.Valiant.Mapping`
- `StaticNorth.Valiant.Endpoints.AspNetCore`
- `StaticNorth.Valiant.Settings`
- `StaticNorth.Valiant.Settings.AspNetCore`
- `StaticNorth.Valiant.Settings.Validation`

`StaticNorth.Valiant.Endpoints` is a non-packable implementation project. Its contract
assembly is bundled into `StaticNorth.Valiant.Endpoints.AspNetCore`.

Shared package metadata, README/icon assets, symbol settings, output paths, and
structural package validation are centralized in `Directory.Build.props` and
`Directory.Build.targets`. Package-specific descriptions, tags, target
frameworks, AOT claims, content files, and custom analyzer pack targets remain
in the owning project.

`StaticNorth.Valiant.Pack.proj` is the authoritative publish manifest. Its verifier asserts
the ten expected NuGet and symbol packages, package metadata, dependencies,
analyzers, `buildTransitive`/content assets, and the bundled endpoint contracts.
Leaf-only consumer applications verify transitive package and analyzer delivery.

CI runs only for pushes to `develop`. Publishing is owned by
`.github/workflows/release.yml`, which runs when a GitHub Release is published.
Release tags must be SemVer with an optional leading `v`; the normalized tag
version is applied to compiled assemblies and NuGet packages. Releases verify
and publish NuGet packages only; documentation publication is independent.
Release restore, build, test, and audit commands use `StaticNorth.Valiant.Release.slnf`.
Keep that filter synchronized with every `.csproj` in `StaticNorth.Valiant.sln` and exclude
the documentation `.esproj`, so releases cannot invoke npm or deploy the site.

Stable documentation uses committed Docusaurus snapshots with one numeric entry
per SemVer major. Major `N` represents the latest released `N.x` docs. A
manual **Publish Docs** workflow reads the active major from
`docs/current-major.json`, pins the current `main` SHA, generates one
snapshot containing the Validation, Endpoints, Mapping, and Settings sections,
imports only that major into the complete history on `main`, commits the
generated files, idempotently ensures the `valiant-docs` Cloudflare Pages
project exists, and deploys the exact persisted commit. If `main` moves
before persistence, publication fails and must be rerun instead of layering a
stale snapshot onto the new head. The Cloudflare API token must target the
account—not a domain or zone—and grant Account > Cloudflare Pages > Edit. All
four sections share one inventory and version selector. These commands remain
available for local preview and diagnosis:

```bash
cd docs
npm run docs:current-major
npm run docs:snapshot-major -- N
npm run docs:validate-major -- N
npm run build
```

The docs navbar intentionally keeps its version selector as a dropdown even
when only one released major exists. Docusaurus normally collapses that case to
a plain link, so the site owns a narrow 3.10.2 theme override at
`docs/src/theme/NavbarItem/DocsVersionDropdownNavbarItem`. Diff that file
against upstream whenever the pinned Docusaurus version changes.

Package sources are built from the immutable release tag. The production docs
artifact is built separately from the exact bot commit on `main`, which
holds the complete major-version history. Change `docs/current-major.json` in a
reviewed commit before publishing a new major.
Repository rules must allow `github-actions[bot]` to push the allowlisted
generated docs files to `main`; signed-commit, required-PR, or status-check
rules need a narrow bypass or a narrowly scoped GitHub App credential. Renaming
the release workflow requires a matching update to the NuGet.org
trusted-publishing policy.

MediatR package note:
- `MediatR` is intentionally pinned to `[12.5.0]`, the final free MediatR release.

## Local Conventions

- Prefer small, scoped changes.
- Preserve public API compatibility unless the user explicitly asks for a breaking change.
- Keep generated-source behavior deterministic.
- Do not add reflection-based assembly scanning; generated DI is a core design point.
- Do not remove `netstandard2.0` support casually. The core package targets `net8.0;netstandard2.0`.
- Generated code should use fully qualified names where existing emission does.
- Before editing source generator internals, inspect existing parser/emitter helpers and follow local patterns.

## Known Environment Notes

- `RTK.md` and `AGENTS.md` are repo-root documentation for agents.
- `dotnet build StaticNorth.Valiant.sln` currently passes after NuGet icon metadata changes.
- `assets/logo.png` is expected to be committed with the package metadata changes.
- Process listing may not be available in the sandbox.
