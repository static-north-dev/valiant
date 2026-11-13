---
title: Runtime and generation
description: Validator APIs, modes, Runtime Async opt-in, failure behavior, DI lifetime, generator scope, and diagnostics.
---

# Runtime and generation

## Validator API

Generated validators implement `IValidator<T>`:

```csharp
ValidationResult Validate(T instance);
ValueTask<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default);
bool IsValid(T instance);
ValueTask<bool> IsValidAsync(T instance, CancellationToken cancellationToken = default);
```

Use async APIs whenever a validator graph can contain async predicates or async property validators.

## Opt in to Runtime Async on .NET 11

With the existing Valiant NuGet package reference, add this to the `.csproj` that declares your `[ValiantValidator]` classes. If your validators live in a class library, configure that library. The condition enables the compiler feature for `net11.0` builds, including in a multi-targeted project.

```xml title="YourProject.csproj"
<PropertyGroup Condition="'$(TargetFramework)' == 'net11.0'">
  <Features>$(Features);runtime-async=on</Features>
</PropertyGroup>
```

Rebuild the project to apply the setting. The NuGet package delivers the Valiant source generator, which emits validator code during your build. The compiler flag applies to eligible async methods in that project, including generated `ValidateAsync` and `IsValidAsync` methods and your own async code. Precompiled Valiant assemblies, including the ASP.NET Core endpoint filter and the validation runtime, use the settings from their own builds.

See [Microsoft’s Runtime Async documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime#runtime-async) for the compiler feature details.

## Options

| Option | Values | Default |
| --- | --- | --- |
| `Mode` | `Auto`, `Debuggable`, `Fast` | `Auto` |
| `FailureBehavior` | `ValidateAll`, `FailFast` | `ValidateAll` |
| `Lifetime` | `Scoped`, `Transient`, `Singleton` | `Scoped` |

`Auto` uses the runtime, debuggable backend in Debug builds and generated fast code otherwise. `Debuggable` lets you step through `Configure`; `Fast` always uses direct generated checks.

Choose validator lifetime based on constructor dependencies. A singleton validator must not capture scoped services.

## Result model

`ValidationResult` exposes all failures as well as `Errors`, `Warnings`, and `Infos`. Only error severity makes `IsValid` false. Each `ValidationFailure` includes `PropertyName`, `ErrorCode`, `ErrorMessage`, and `Severity`.

Results are immutable and do not need disposal. In `Fast` mode, simple validators
with at most six checks and fixed default failure content reuse results after
each failure combination is first seen. All checks still run on every call.
Reuse is bounded to 63 nonempty combinations per validator type. Custom diagnostic
expressions, culture-dependent messages, composition, async checks, and larger
validators keep the owned-result path.

## Generator scope

`[ValiantValidator]` is the discovery hook. Only marked validator declarations enter the incremental pipeline. Generated output can include:

- direct sync/async `IValidator<T>` methods;
- metadata used by OpenAPI and integrations;
- generated DI registration through `AddValiantValidators`;
- composition code for root and property validators.

The marker accepts generic and `typeof(...)` compatibility forms. Prefer an ordinary `[ValiantValidator]` declaration that inherits `ValidatorDefinition<T>`; it is the recommended authoring surface for both fluent and attribute-based validation.

## Authoring constraints

- Validator and generated containing types must be `partial`.
- Attribute rules must be Valiant validation attributes.
- Generator-readable expressions produce direct fast-path code; unsupported declaration shapes are compile-time diagnostics rather than runtime surprises.
- Property validators skip null property values unless presence is checked separately.
