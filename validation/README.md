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

Small generated validators with at most six checks and fixed default failure
content reuse immutable results after each failure combination is first seen.
Every check still runs on each call. Reuse requires no disposal and retains at
most 63 nonempty combinations per validator type. Custom diagnostic expressions,
culture-dependent messages, composition, async checks, and larger validators keep
the owned-result path.

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

## Performance

`StaticNorth.Valiant.Validation` benchmarks its generated `Fast` path against
the pinned Validly 1.3.0 control. The workload applies five equivalent property
rules to valid, one-failure, and all-failure inputs. It measures both a
materialized diagnostic result and the separate status-only API.

This local reference snapshot was captured on 2026-09-26 with BenchmarkDotNet
0.15.8 and .NET 10.0.11 Arm64, using five warmups and ten measured iterations
in the in-process runner. Lower ratios are faster.

| Covered operation | Valiant | Validly | Valiant / Validly | Allocation, both implementations |
| --- | ---: | ---: | ---: | ---: |
| Full result, valid input | 2.530 ns | 56.698 ns | 0.04 | 0 B |
| Full result, one failure | 3.457 ns | 58.104 ns | 0.06 | 0 B |
| Full result, all failures | 4.927 ns | 69.369 ns | 0.07 | 0 B |

These measurements include the full result API after warmup: this small validator
reuses immutable failure content, while Validly reuses pooled storage and requires
disposal. First-use allocation is excluded. Results are workload- and
machine-dependent, not a performance guarantee. See the
[allocation report](../docs/audits/2026-09-26-validation-allocation-optimization.md)
for changing-input benchmarks, cold-allocation bounds, and eligibility limits.
Refresh or remove this snapshot when the benchmark workload, pinned control,
target runtime, or performance-sensitive implementation changes.

## Benchmarks

The standalone
[`StaticNorth.Valiant.Validation.Benchmarks`](benchmarks/StaticNorth.Valiant.Validation.Benchmarks)
project measures the generated `Fast` path for valid, one-failure, and
all-failure inputs, with Validly as a pinned relative control. Its README
documents correctness checks, the complete benchmark matrix, and the repeatable
before/after workflow:

```bash
dotnet run --project validation/benchmarks/StaticNorth.Valiant.Validation.Benchmarks --configuration Release -- --filter '*'
```
