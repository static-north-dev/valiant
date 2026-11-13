# Valiant Validation

Valiant Validation is a source-generated validation library for .NET. It keeps validation authoring small and explicit while generating direct runtime code that is friendly to trimming, Native AOT, and dependency injection without reflection-based assembly scanning.

## Packages

- `StaticNorth.Valiant.Validation` provides the runtime API and source generator.
- `StaticNorth.Valiant.Validation.AspNetCore` adds Minimal API, MVC, and problem-details integration.
- `StaticNorth.Valiant.Validation.AspNetCore.OpenApi` adds native ASP.NET Core OpenAPI schema integration.
- `StaticNorth.Valiant.Validation.MediatR` adds a MediatR pipeline behavior. It intentionally pins MediatR to `12.5.0`, the final free MediatR release.
- `StaticNorth.Valiant.Validation.Mediator` adds the equivalent behavior for the source-generated Mediator package.

Install the most specific package your application uses. Integration packages forward `StaticNorth.Valiant.Validation` runtime, build, and analyzer assets, so a separate `StaticNorth.Valiant.Validation` package reference is not required for validator generation. Reference multiple integration packages when using multiple integrations, such as ASP.NET Core validation and OpenAPI.

## Quick Start

```csharp
using StaticNorth.Valiant.Validation;

[ValiantValidator]
public sealed partial class CreateUserValidator : ValidatorDefinition<CreateUser>
{
    public override void Configure(ValidationContext<CreateUser> context)
    {
        context.Property(user => user.Name)
            .Required()
            .MaxLength(64);

        context.Property(user => user.Age)
            .GreaterThanOrEqualTo(18);
    }
}
```

The generator emits an `IValidator<CreateUser>` implementation that runs either generated checks or the fluent runtime. When `Microsoft.Extensions.DependencyInjection` is available, it also emits `AddValiantValidators(...)`:

```csharp
services.AddValiantValidators(options =>
{
    options.Mode = ValiantValidationMode.Auto;
    options.Lifetime = ValiantValidatorLifetime.Scoped;
    options.FailureBehavior = ValiantFailureBehavior.ValidateAll;
});
```

## Validation Status

Use `Validate(...)` or `ValidateAsync(...)` to get the complete diagnostic
result. `IsValid(...)` and `IsValidAsync(...)` run the same rules, evaluate the
same diagnostic expressions, and call the same methods on reused validators.
The fluent runtime can return the validity of the collected failures directly,
without constructing the final diagnostic result.

Attribute validators always use generated checks. Eligible fluent validators
use generated checks in `Auto` and `Fast`, in Debug and Release builds. A fluent
validator qualifies when it is sealed and directly
overrides `Configure` with straight-line rules on direct members of a
reference type. The generated subset covers built-in string, null, and
`int` or `int?` comparison rules with default diagnostics and nonnegative
integer literals for bounds. It also supports
`context.Property(x => x.Child).Children(child => child.UseValidator(_child))`
when `_child` is a readonly `IValidator<TChild>` field. Other fluent validators execute `Configure`
through `ValidationContext<T>` in every mode. Explicit `Debuggable` mode
runs the original `Configure` for eligible fluent validators too, preserving
the same results and rule order.

Small generated validators with at most six checks and fixed default failure
content reuse immutable results after each failure combination is first seen.
Each call evaluates the applicable rules again. Reuse requires no disposal
and retains at most 63 nonempty combinations per validator type.

## Fluent Rules

Validators inherit `ValidatorDefinition<T>` and override `Configure` to declare rules through `ValidationContext<T>`:

```csharp
public override void Configure(ValidationContext<CreateUser> context)
{
    if (context.Instance.RequiresInvite)
    {
        context.Property(user => user.InviteCode).Required();
    }

    context.Property(user => user.Email)
        .Required()
        .MaxLength(254);
}
```

Set `Mode = ValiantValidationMode.Debuggable` to step into the original `Configure` method of a simple fluent validator. Validators with control flow execute `Configure` at runtime in every mode.

## Attribute Rules

Validators can also derive from `ValidatorDefinition<T>` when model attributes should define the default rules:

```csharp
using StaticNorth.Valiant.Validation;

[ValiantValidator]
public sealed partial class CreateUserValidator : ValidatorDefinition<CreateUser>;

public sealed class CreateUser
{
    [Required("NameRequired", "Name is required.")]
    [MaxLength(64)]
    public string? Name { get; init; }
}
```

Validation attributes are Valiant attributes, not `System.ComponentModel.DataAnnotations`. They use the same rule names and generated logic as fluent rules.

## Reuse

Whole-model validators can be reused from another validator:

```csharp
public void Configure(ValidationContext<CreateUser> context)
{
    context.UseValidator(_nameValidator);
    context.Property(user => user.Age).GreaterThanOrEqualTo(18);
}
```

Property validators derive from `PropertyValidatorDefinition<TProperty>` and can be applied to property values:

```csharp
public void Configure(ValidationContext<CreateUser> context)
{
    context.Property(user => user.Address).UseValidator(_addressValidator);
}
```

Property validators skip null property values. Add `Required()` before `UseValidator(...)` when the property itself must be present.

## ASP.NET Core

`StaticNorth.Valiant.Validation.AspNetCore` integrates generated validators with Minimal APIs, MVC, and validation problem responses.

```csharp
builder.Services.AddValiantValidators();
builder.Services.AddValiantMvcValidation();
```

`StaticNorth.Valiant.Validation.AspNetCore.OpenApi` maps generated validation metadata into native ASP.NET Core OpenAPI schema constraints and `x-valiant-rules` extensions:

```csharp
builder.Services.AddValiantValidators();
builder.Services.AddValiantOpenApi();
```

## Mediator

`StaticNorth.Valiant.Validation.MediatR` and `StaticNorth.Valiant.Validation.Mediator` add pipeline behavior integration:

```csharp
services.AddMediatR(configuration =>
{
    configuration.AddValiantValidation();
});
```

```csharp
services.AddMediator(options =>
{
    options.Assemblies = [typeof(CreateUserCommand).Assembly];
});

services.AddValiantMediatorValidation();
```

For NativeAOT, configure the source-generated Mediator pipeline directly:

```csharp
services.AddMediator(options =>
{
    options.Assemblies = [typeof(CreateUserCommand).Assembly];
    options.PipelineBehaviors = [typeof(ValiantValidationBehavior<,>)];
});
```

Use one Mediator registration path for the Valiant behavior. Do not also call `AddValiantMediatorValidation()` when `ValiantValidationBehavior<,>` is listed in `PipelineBehaviors`.

## Benchmarks

The standalone
[`StaticNorth.Valiant.Validation.Benchmarks`](benchmarks/StaticNorth.Valiant.Validation.Benchmarks)
project measures validation for valid, one-failure, and all-failure inputs,
with Validly as a pinned relative control. Its README
documents correctness checks, the complete benchmark matrix, and the repeatable
before/after workflow:

```bash
dotnet run --project validation/benchmarks/StaticNorth.Valiant.Validation.Benchmarks --configuration Release -- --filter '*'
```
