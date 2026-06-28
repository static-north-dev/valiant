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

Validators can also be reused for a property value. This is useful for nested objects such as addresses. The child validator is a normal Valiant validator, so it is discovered and registered in DI the same way as every other validator:

```csharp
[ValiantValidator]
public sealed partial class AddressValidator : IValidatorDefinition<Address>
{
    public static void Configure(ValidationContext<Address> context)
    {
        context.Property(address => address.Line1).Required();
        context.Property(address => address.Zip).Required();
    }
}

[ValiantValidator]
public sealed partial class CreateUserValidator : IValidatorDefinition<CreateUser>
{
    private readonly AddressValidator _addressValidator;

    public CreateUserValidator(AddressValidator addressValidator)
    {
        _addressValidator = addressValidator;
    }

    public void Configure(ValidationContext<CreateUser> context)
    {
        context.Property(user => user.Address).UseValidator(_addressValidator);
    }
}
```

Property validators skip null property values. Add `Required()` before `UseValidator(...)` when the property itself must be present. Child failures are reported under the parent path, for example `Address.Line1`.

The generator emits an `IValidator<CreateUser>` implementation with direct property reads and native branch checks. When `Microsoft.Extensions.DependencyInjection` is available in the consumer project, it also emits:

```csharp
services.AddValiantValidations(options =>
{
    options.Mode = ValiantValidationMode.Auto;
    options.Lifetime = ValiantValidatorLifetime.Singleton;
    options.FailureBehavior = ValiantFailureBehavior.ValidateAll;
});
```

That generated extension registers validators without reflection scanning and configures the validation backend:

- `Auto` runs the debuggable runtime backend in Debug builds and the generated fast backend otherwise.
- `Debuggable` always executes the user-authored `Configure` method, so normal breakpoints work.
- `Fast` always uses the source-generated direct validation code.
- `Lifetime` controls DI registration for generated validators: `Singleton`, `Scoped`, or `Transient`.
- `FailureBehavior` controls whether validators collect every failure or stop after the first one.

The attribute is the generator discovery hook; `IValidatorDefinition<T>` supplies the model type, and the generator validates the `Configure` declaration shape.

The older `[ValiantValidator(typeof(CreateUser))]` and `[ValiantValidator<CreateUser>]` forms are still supported for compatibility, but the marker attribute is the preferred API when the validator implements `IValidatorDefinition<T>`.

Validators expose both result and boolean APIs:

```csharp
ValidationResult result = await validator.ValidateAsync(model);
result.ThrowIfFailed();

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
- `DefinedEnum()`
- `EnumName<TEnum>()`
- `EnumValue<TEnum>()`
- `LengthBetween(min, max)`
- `MinLength(min)`
- `MaxLength(max)`
- `Count(count)`
- `CountBetween(min, max)`
- `MinCount(min)`
- `MaxCount(max)`
- `Contains(item)`
- `Excludes(item)`
- `All(predicate)`
- `Any(predicate)`
- `None(predicate)`
- `HasKey(key)`
- `HasValue(value)`
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

Type-specific checks are exposed only where they make sense. For example, `NotNull()` and `Null()` are available on reference and nullable value properties, string length checks are available on string properties, `DefinedEnum()` is available on enum properties, collection count, membership, and predicate checks are available on collection properties, and dictionary key/value checks are available on dictionary properties.

Each validator call accepts optional `code` and `message` arguments for that exact check:
It also accepts an optional `severity`; `Error` failures make the result invalid, while `Warning` and `Info` failures are reported without making `IsValid` false.

```csharp
context.Property(user => user.Name)
    .Required(code: "NameRequired", message: "Name is required.")
    .MaxLength(
        64,
        code: "NameTooLong",
        message: "Name must be 64 characters or fewer.",
        severity: ValiantSeverity.Warning);

ValidationResult result = validator.Validate(user);
IReadOnlyList<ValidationFailure> allFailures = result.Failures;
IReadOnlyList<ValidationFailure> errors = result.Errors;
IReadOnlyList<ValidationFailure> warnings = result.Warnings;
```

Failure behavior can be configured globally, per validator, or for one property chain. The narrower setting wins:

```csharp
services.AddValiantValidations(options =>
{
    options.FailureBehavior = ValiantFailureBehavior.FailFast;
});

public static void Configure(ValidationContext<CreateUser> context)
{
    context.ValidateAll();

    context.Property(user => user.Name)
        .FailFast()
        .Required()
        .MaxLength(64);

    context.Property(user => user.Email).Required();
}
```

For strings, the presence checks are intentionally distinct:

| Check | `null` | `""` | `"   "` |
|---|---:|---:|---:|
| `NotNull()` | fail | pass | pass |
| `NotEmpty()` | fail | fail | pass |
| `Required()` | fail | fail | fail |

Enum checks cover the common enum shapes:

```csharp
context.Property(order => order.Status).DefinedEnum();
context.Property(order => order.StatusName).EnumName<OrderStatus>();
context.Property(order => order.StatusValue).EnumValue<OrderStatus>();
```

Native loops are supported in `Configure`, including `foreach`, `for`, `while`, and `do`/`while`:

```csharp
foreach (var item in context.Instance.Items)
{
    if (item.RequiresName)
    {
        context.Property(order => order.Name).Required();
    }
}
```

Collection and dictionary checks cover exact/ranged counts, membership, predicates, and dictionary keys/values:

```csharp
context.Property(order => order.Items)
    .NotEmpty()
    .Count(3)
    .CountBetween(1, 25)
    .Contains(requiredItem)
    .All(item => item.Quantity > 0)
    .None(item => item.IsDeleted);

context.Property(order => order.Metadata)
    .HasKey("source")
    .HasValue("api");
```

Collections can also validate each item with an item `ValidationContext<TItem>`. The generated fast validator emits a real loop and reports indexed property paths such as `Items[0].Sku`:

```csharp
context.Property(order => order.Items).ForEach(item =>
{
    item.Property(x => x.Sku).Required();
});

context.Property(order => order.Items).ForEach((item, items) =>
{
    if (items.Count > 0)
    {
        item.Property(x => x.Sku).Required();
    }
});
```

Native C# loops can validate a specific collection item by creating an item context with the item, the owning collection selector, and the current index:

```csharp
var index = 0;
foreach (var item in context.Instance.Items)
{
    context.Item(item, order => order.Items, index)
        .Property(x => x.Sku)
        .Required();

    index++;
}
```

## ASP.NET Core integration

`Valiant.AspNetCore` contains ASP.NET-specific helpers so the core package can stay free of ASP.NET references.

Minimal APIs can validate endpoint models with an endpoint filter:

```csharp
using Valiant.AspNetCore;

app.MapPost("/users", (CreateUser user) => Results.Ok())
    .WithValiantValidation<CreateUser>();
```

`WithValiantValidation<T>()` validates endpoint arguments assignable to `T`. `WithValiantValidation()` validates all endpoint arguments that have registered `IValidator<T>` services.
Both extensions add ASP.NET Core `ProducesValidationProblem(400)` endpoint metadata, so the built-in .NET OpenAPI generator can document validation problem responses without Swashbuckle or NSwag-specific hooks.

MVC can add Valiant validators to the normal model validation pipeline:

```csharp
builder.Services
    .AddControllers()
    .AddValiantValidation();
```

or:

```csharp
builder.Services.AddValiantMvcValidation();
```

With `[ApiController]`, MVC turns model-state validation failures into the usual validation problem response.

Validation results can also be converted directly to problem-details shapes:

```csharp
ValidationResult result = validator.Validate(model);

IDictionary<string, string[]> errors = result.ToValidationProblemDictionary();
HttpValidationProblemDetails httpProblem = result.ToHttpValidationProblemDetails();
ValidationProblemDetails mvcProblem = result.ToMvcValidationProblemDetails();
```

## Analyzer suggestions

The source generator reports compiler diagnostics for invalid validator declarations and low-noise suggestions for suspicious rule chains:

- `VLT101` warns when a check is redundant because another check in the same chain already covers it, such as `Required().NotNull()`.
- `VLT102` warns when the same non-custom check is declared more than once in a property chain.
- `VLT103` warns when a property chain sets failure behavior more than once, because only the last `FailFast()` or `ValidateAll()` setting is used.

## Repository layout

- `src/Valiant` contains runtime contracts and the NuGet package metadata.
- `src/Valiant.AspNetCore` contains Minimal API, MVC, and problem details integrations.
- `src/Valiant.SourceGeneration` contains the incremental source generator packaged as a NuGet analyzer.
- `samples/Valiant.Sample` exercises generated validation and generated DI registration.

## Local verification

```bash
dotnet build Valiant.sln
dotnet run --project samples/Valiant.Sample/Valiant.Sample.csproj
dotnet test --project tests/Valiant.Tests/Valiant.Tests.csproj
dotnet pack src/Valiant/Valiant.csproj -c Release
```
