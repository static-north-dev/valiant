# Valiant

Valiant is an experimental .NET validation library with a property-focused authoring surface and source-generated runtime implementation.

The goal is to keep the developer API small while generating direct validation code that is friendly to trimming, Native AOT, and dependency injection without reflection-based assembly scanning.

## Current shape

```csharp
using Valiant;

[ValiantValidator]
public sealed partial class CreateUserValidator : IValidatorDefinition<CreateUser>
{
    public static void Configure(ValidationContext<CreateUser> context)
    {
        context.Property(user => user.Name)
            .Required()
            .MaxLength(64)
            .Satisfies(
                static name => name is null || !name.Contains('!'),
                code: "NameContainsBang",
                message: "Name must not contain exclamation marks.");

        context.Property(user => user.Age)
            .GreaterThanOrEqualTo(18)
            .SatisfiesAsync(
                static (age, _) => new ValueTask<bool>(age != 42),
                code: "ReservedAge",
                message: "Age 42 is reserved.");
    }
}
```

Native C# conditionals can be used to make validations conditional. The generator rewrites `context.Instance` references to the runtime model instance:

```csharp
public static void Configure(ValidationContext<CreateUser> context)
{
    if (context.Instance.RequiresInvite)
    {
        context.Property(user => user.InviteCode).Required();
    }
    else
    {
        context.Property(user => user.Age).GreaterThanOrEqualTo(18);
    }

    switch (context.Instance.Kind)
    {
        case UserKind.Admin:
            context.Property(user => user.AdminCode).Required();
            break;
        default:
            break;
    }
}
```

In `Debuggable` mode, the user-authored `Configure` method executes at runtime, so breakpoints in native `if`/`else` and `switch` statements work normally.


Whole-model validators can be reused from another validator for the same model type. Inject the validator through the normal constructor and pass that dependency to `UseValidator`; generated DI registers concrete validators so this resolves out of the box:

```csharp
[ValiantValidator]
public sealed partial class CreateUserNameValidator : IValidatorDefinition<CreateUser>
{
    public static void Configure(ValidationContext<CreateUser> context)
    {
        context.Property(user => user.Name).Required();
    }
}

[ValiantValidator]
public sealed partial class CreateUserValidator : IValidatorDefinition<CreateUser>
{
    private readonly CreateUserNameValidator _nameValidator;

    public CreateUserValidator(CreateUserNameValidator nameValidator)
    {
        _nameValidator = nameValidator;
    }

    public void Configure(ValidationContext<CreateUser> context)
    {
        context.UseValidator(_nameValidator);
        context.Property(user => user.Age).GreaterThanOrEqualTo(18);
    }
}
```

The generator emits an `IValidator<CreateUser>` implementation with direct property reads and native branch checks. When `Microsoft.Extensions.DependencyInjection` is available in the consumer project, it also emits:

```csharp
services.AddValiantValidations(options =>
{
    options.Mode = ValiantValidationMode.Auto;
    options.Lifetime = ValiantValidatorLifetime.Singleton;
});
```

That generated extension registers validators without reflection scanning and configures the validation backend:

- `Auto` runs the debuggable runtime backend in Debug builds and the generated fast backend otherwise.
- `Debuggable` always executes the user-authored `Configure` method, so normal breakpoints work.
- `Fast` always uses the source-generated direct validation code.
- `Lifetime` controls DI registration for generated validators: `Singleton`, `Scoped`, or `Transient`.

The attribute is the generator discovery hook; `IValidatorDefinition<T>` supplies the model type, and the generator validates the `Configure` declaration shape.

The older `[ValiantValidator(typeof(CreateUser))]` and `[ValiantValidator<CreateUser>]` forms are still supported for compatibility, but the marker attribute is the preferred API when the validator implements `IValidatorDefinition<T>`.

Validators expose both result and boolean APIs:

```csharp
ValidationResult result = await validator.ValidateAsync(model);
bool syncOnlyIsValid = validator.IsValid(model);
bool isValid = await validator.IsValidAsync(model);
```

`ValidateAsync` and `IsValidAsync` run both synchronous and asynchronous validations. If a validator contains `SatisfiesAsync` checks, calling `Validate` or `IsValid` throws and directs callers to the async API.

## Supported property checks

- `NotNull()`
- `Null()`
- `Required()`
- `NotEmpty()`
- `Empty()`
- `LengthBetween(min, max)`
- `MinLength(min)`
- `MaxLength(max)`
- `GreaterThan(value)`
- `GreaterThanOrEqualTo(value)`
- `LessThan(value)`
- `LessThanOrEqualTo(value)`
- `Equal(value)`
- `NotEqual(value)`
- `InclusiveBetween(from, to)`
- `ExclusiveBetween(from, to)`
- `Satisfies(predicate)`
- `SatisfiesAsync(predicate)`

Each validator call accepts optional `code` and `message` arguments for that exact check:

```csharp
context.Property(user => user.Name)
    .Required(code: "NameRequired", message: "Name is required.")
    .MaxLength(64, code: "NameTooLong", message: "Name must be 64 characters or fewer.");
```

For strings, the presence checks are intentionally distinct:

| Check | `null` | `""` | `"   "` |
|---|---:|---:|---:|
| `NotNull()` | fail | pass | pass |
| `NotEmpty()` | fail | fail | pass |
| `Required()` | fail | fail | fail |

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
