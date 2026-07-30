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

The generator emits an `IValidator<CreateUser>` implementation with direct property reads and native branch checks. When `Microsoft.Extensions.DependencyInjection` is available, it also emits `AddValiantValidators(...)`:

```csharp
services.AddValiantValidators(options =>
{
    options.Mode = ValiantValidationMode.Auto;
    options.Lifetime = ValiantValidatorLifetime.Scoped;
    options.FailureBehavior = ValiantFailureBehavior.ValidateAll;
});
```

## Validation Status

Use `Validate(...)` or `ValidateAsync(...)` when callers need failure codes,
messages, property names, and severity partitions. In `Fast` mode, generated
validators build that diagnostic result lazily and avoid allocating a result
builder for valid inputs.

Use `IsValid(...)` or `IsValidAsync(...)` when callers only need the validity
status. The generated `Fast` path evaluates rule conditions, predicates,
severities, and reused validators directly without constructing failure
objects. Because diagnostics are not requested, failure code, message, and
property-name expressions are not evaluated. Warning-only results are valid;
errors and unknown severity values are invalid.

`Debuggable` mode keeps the materialized runtime path so user-authored
configuration and diagnostic expressions remain available to the debugger.

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

In `Debuggable` mode, the user-authored `Configure` method executes at runtime, so breakpoints in native `if`, `else`, and `switch` statements work normally.

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
project measures the generated `Fast` path for valid, one-failure, and
all-failure inputs, with Validly as a pinned relative control. Its README
documents correctness checks and the repeatable before/after workflow.
