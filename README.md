# Valiant

Valiant is an experimental .NET validation library with a FluentValidation-style authoring surface and source-generated runtime implementation.

The goal is to keep the developer API small while generating direct validation code that is friendly to trimming, Native AOT, and dependency injection without reflection-based assembly scanning.

## Current shape

```csharp
using Valiant;

[ValiantValidator]
public sealed partial class CreateUserValidator : IValidatorDefinition<CreateUser>
{
    public static ValidationContext<CreateUser> Configure(ValidationContext<CreateUser> context)
    {
        context.RuleFor(user => user.Name)
            .NotEmpty()
            .MaximumLength(64)
            .Must(static name => name is null || !name.Contains('!'))
            .WithMessage("Name must not contain exclamation marks.");

        context.RuleFor(user => user.Age)
            .GreaterThanOrEqualTo(18)
            .MustAsync(static (age, _) => new ValueTask<bool>(age != 42))
            .WithMessage("Age 42 is reserved.");

        return context;
    }
}
```

Native C# conditionals can be used to make rules conditional. The generator rewrites `context.Instance` references to the runtime model instance:

```csharp
public static ValidationContext<CreateUser> Configure(ValidationContext<CreateUser> context)
{
    if (context.Instance.RequiresInvite)
    {
        context.RuleFor(user => user.InviteCode).NotEmpty();
    }
    else
    {
        context.RuleFor(user => user.Age).GreaterThanOrEqualTo(18);
    }

    switch (context.Instance.Kind)
    {
        case UserKind.Admin:
            context.RuleFor(user => user.AdminCode).NotEmpty();
            break;
        default:
            break;
    }

    return context;
}
```

The generator emits an `IValidator<CreateUser>` implementation with direct property reads and native branch checks. When `Microsoft.Extensions.DependencyInjection` is available in the consumer project, it also emits:

```csharp
services.AddValiantValidators();
```

That generated extension registers validators without reflection scanning. The attribute is the generator discovery hook; `IValidatorDefinition<T>` supplies the model type and gives the validator a compiler-checked rule declaration shape.

The older `[ValiantValidator(typeof(CreateUser))]` and `[ValiantValidator<CreateUser>]` forms are still supported for compatibility, but the marker attribute is the preferred API when the validator implements `IValidatorDefinition<T>`.

Validators expose both result and boolean APIs:

```csharp
ValidationResult result = await validator.ValidateAsync(model);
bool syncOnlyIsValid = validator.IsValid(model);
bool isValid = await validator.IsValidAsync(model);
```

`ValidateAsync` and `IsValidAsync` run both synchronous and asynchronous rules. If a validator contains `MustAsync` rules, calling `Validate` or `IsValid` throws and directs callers to the async API.

## Supported rule calls

- `NotNull()`
- `Null()`
- `NotEmpty()`
- `Empty()`
- `Length(min, max)`
- `MinimumLength(min)`
- `MaximumLength(max)`
- `GreaterThan(value)`
- `GreaterThanOrEqualTo(value)`
- `LessThan(value)`
- `LessThanOrEqualTo(value)`
- `Equal(value)`
- `NotEqual(value)`
- `InclusiveBetween(from, to)`
- `ExclusiveBetween(from, to)`
- `Must(predicate)`
- `MustAsync(predicate)`
- `WithMessage("constant message")`

## Repository layout

- `src/Valiant` contains runtime contracts and the NuGet package metadata.
- `src/Valiant.SourceGeneration` contains the incremental source generator packaged as a NuGet analyzer.
- `samples/Valiant.Sample` exercises generated validation and generated DI registration.

## Local verification

```bash
dotnet build Valiant.sln
dotnet run --project samples/Valiant.Sample/Valiant.Sample.csproj
dotnet test --project tests/Valiant.Tests/Valiant.Tests.csproj
dotnet pack src/Valiant/Valiant.csproj -c Release
```
