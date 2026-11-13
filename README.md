# Valiant

Valiant is a family of source-generated .NET libraries for validation, Minimal API endpoints, object mapping, and strongly typed settings.

The goal is to keep developer APIs explicit while generating direct runtime code and deterministic metadata that are friendly to trimming, Native AOT, and dependency injection without reflection-based assembly scanning.

## Packages

- `StaticNorth.Valiant.Validation` provides the runtime API and source generator.
- `StaticNorth.Valiant.Validation.AspNetCore` adds Minimal API, MVC, and problem-details integration.
- `StaticNorth.Valiant.Validation.AspNetCore.OpenApi` adds native ASP.NET Core OpenAPI schema integration.
- `StaticNorth.Valiant.Validation.MediatR` adds a MediatR pipeline behavior. It intentionally pins MediatR to `12.5.0`, the final free MediatR release.
- `StaticNorth.Valiant.Validation.Mediator` adds the equivalent behavior for the source-generated Mediator package.
- `StaticNorth.Valiant.Endpoints.AspNetCore` provides endpoint-per-class Minimal APIs, generated route registration, groups, conventions, and query binding.
- `StaticNorth.Valiant.Mapping` provides source-generated object mapping.
- `StaticNorth.Valiant.Settings` provides source-generation primitives for strongly typed settings schemas.
- `StaticNorth.Valiant.Settings.AspNetCore` adds a built-in ASP.NET Core UI for visualizing generated settings schemas.
- `StaticNorth.Valiant.Settings.Validation` adapts Valiant validators to `IValidateOptions<TOptions>` for generated settings models.

Integration packages are standalone installation points: install the most specific package or packages your application uses. Their parent runtime, build, and source-generator assets flow transitively; add parent packages directly only when you want to use that parent package without an integration.

The full module guides live in the [Docusaurus documentation site](docs/README.md).

## Quick start

```csharp
using StaticNorth.Valiant.Validation;

[ValiantValidator]
public sealed partial class CreateUserValidator : ValidatorDefinition<CreateUser>
{
    public override void Configure(ValidationContext<CreateUser> context)
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
public override void Configure(ValidationContext<CreateUser> context)
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
public sealed partial class CreateUserNameValidator : ValidatorDefinition<CreateUser>
{
    public override void Configure(ValidationContext<CreateUser> context)
    {
        context.Property(user => user.Name).Required();
    }
}

[ValiantValidator]
public sealed partial class CreateUserValidator : ValidatorDefinition<CreateUser>
{
    private readonly CreateUserNameValidator _nameValidator;

    public CreateUserValidator(CreateUserNameValidator nameValidator)
    {
        _nameValidator = nameValidator;
    }

    public override void Configure(ValidationContext<CreateUser> context)
    {
        context.UseValidator(_nameValidator);
        context.Property(user => user.Age).GreaterThanOrEqualTo(18);
    }
}
```

Property validators can be reused for a property value. This is useful for nested objects such as addresses. Property validators derive from `PropertyValidatorDefinition<TProperty>` and are discovered for DI when they are marked with `[ValiantValidator]`. Override `Validate(...)`, `ValidateAsync(...)`, or both:

```csharp
[ValiantValidator]
public sealed class AddressPropertyValidator : PropertyValidatorDefinition<Address>
{
    public override ValidationResult Validate(Address value)
    {
        // Return failures for invalid address properties.
        return ValidationResult.Valid;
    }
}

[ValiantValidator]
public sealed partial class CreateUserValidator : ValidatorDefinition<CreateUser>
{
    private readonly AddressPropertyValidator _addressValidator;

    public CreateUserValidator(AddressPropertyValidator addressValidator)
    {
        _addressValidator = addressValidator;
    }

    public override void Configure(ValidationContext<CreateUser> context)
    {
        context.Property(user => user.Address).UseValidator(_addressValidator);
    }
}
```

Property validators skip null property values. Add `Required()` before `UseValidator(...)` when the property itself must be present. Child failures are reported under the parent path, for example `Address.Line1`.

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

Validation attributes are Valiant attributes, not `System.ComponentModel.DataAnnotations`. They use the same rule names and generated logic as fluent rules. Optional `Code`, `Message`, and `Severity` metadata can be supplied through constructors or named properties. Conditional attributes use a static model predicate by name:

```csharp
[Required(ConditionMethod = nameof(ShouldValidateName))]
public string? Name { get; init; }

public static bool ShouldValidateName(CreateUser user) => user.Name != "skip";
```

Attribute-based models can also reuse a parameterless property validator:

```csharp
[UseValidator<AddressPropertyValidator>]
public Address? Address { get; init; }
```

`ValidatorDefinition<T>` authoring is intentionally mutually exclusive: omit `Configure(ValidationContext<T>)` to use model validation attributes, or declare `Configure` to use fluent rules on a model without validation attributes. If both are present, Valiant reports an analyzer error so rule ownership stays explicit.

The generator emits an `IValidator<CreateUser>` implementation with direct property reads and native branch checks. When `Microsoft.Extensions.DependencyInjection` is available in the consumer project, it also emits:

```csharp
services.AddValiantValidators(options =>
{
    options.Mode = ValiantValidationMode.Auto;
    options.Lifetime = ValiantValidatorLifetime.Scoped;
    options.FailureBehavior = ValiantFailureBehavior.ValidateAll;
});
```

That generated extension registers validators without reflection scanning and configures the validation backend:

- `Auto` runs the debuggable runtime backend in Debug builds and the generated fast backend otherwise.
- `Debuggable` always executes the user-authored `Configure` method, so normal breakpoints work.
- `Fast` always uses the source-generated direct validation code.
- `Lifetime` controls DI registration for generated validators: `Singleton`, `Scoped`, or `Transient`.
- `FailureBehavior` controls whether validators collect every failure or stop after the first one.

The attribute is the generator discovery hook. `ValidatorDefinition<T>` supplies the model type and exposes the overridable `Configure(ValidationContext<T>)` authoring surface for fluent rules. Leaving the override out enables attribute-based default validation.

The older `[ValiantValidator(typeof(CreateUser))]` and `[ValiantValidator<CreateUser>]` forms are still supported for compatibility, but `[ValiantValidator]` with `ValidatorDefinition<T>` is the recommended API.

Generated validators also expose static validation metadata through `IValiantValidationMetadataProvider`. The metadata is registered by `AddValiantValidators` and is used by the OpenAPI package, but it can also be consumed by other tooling.

### Opt in to Runtime Async on .NET 11

Add this to the `.csproj` that declares your `[ValiantValidator]` classes, even if they live in a class library.

```xml
<PropertyGroup Condition="'$(TargetFramework)' == 'net11.0'">
  <Features>$(Features);runtime-async=on</Features>
</PropertyGroup>
```

On rebuild, the setting applies to eligible async methods compiled in that project, including the validators generated by the Valiant NuGet package and your own async methods. Precompiled Valiant assemblies, including the ASP.NET Core endpoint filter, use their own build settings. [Microsoft’s Runtime Async documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime#runtime-async)

## Mapping

Mapping follows the same authoring style as validators: add the marker attribute and implement the definition interface so the generator can infer the source and target types.

```csharp
using StaticNorth.Valiant.Mapping;

[ValiantMapper]
public sealed partial class UserMapper : MapperDefinition<User, UserDto>;
```

`[ValiantMapper]` is the generator discovery hook, while `MapperDefinition<TSource, TTarget>` supplies the mapping pair through `IMapperDefinition<TSource, TTarget>`. The generator emits a direct `Map(User source)` implementation and adds `IMapper<User, UserDto>` to the generated partial type. Public source properties and fields are mapped to public settable target properties and fields by name. Constructor parameters are mapped first, which supports immutable targets and records:

```csharp
public sealed record UserDto(string Name, int Age);
```

Mapper definitions can override `Configure(...)` for explicit member mappings. When `Configure(...)` is present, convention mapping is disabled so rule ownership stays explicit; normal unmapped and incompatible-target diagnostics still apply. `When(...)` makes a member assignment conditional; for constructor parameters, the fallback value is `default`.

```csharp
public sealed partial class UserMapper : MapperDefinition<User, UserDto>
{
    public override void Configure(MappingContext<User, UserDto> context)
    {
        context.Map(source => source.EmailAddress, target => target.Email)
            .When(source => source.IncludeEmail);
    }
}
```

Native `if/else` blocks can branch on `context.Source` and are supported by both generated fast mode and debuggable runtime mode:

```csharp
public sealed partial class UserMapper : MapperDefinition<User, UserDto>
{
    public override void Configure(MappingContext<User, UserDto> context)
    {
        if (context.Source.UseEmail)
        {
            context.Map(source => source.Email, target => target.DisplayName);
        }
        else
        {
            context.Map(source => source.Name, target => target.DisplayName);
        }
    }
}
```

Targets can be ignored explicitly. A conditional ignore can also guard an explicit map for the same target:

```csharp
public sealed partial class UserMapper : MapperDefinition<User, UserDto>
{
    public override void Configure(MappingContext<User, UserDto> context)
    {
        context.Map(source => source.DisplayName, target => target.Name);
        context.Ignore(target => target.Name)
            .When(source => source.HideName);
    }
}
```

Mapper definitions can reuse other mappers declared as dependencies:

```csharp
public sealed partial class UserMapper(AddressMapper addressMapper) : MapperDefinition<User, UserDto>
{
    public override void Configure(MappingContext<User, UserDto> context)
    {
        context.UseMapper<AddressMapper>(addressMapper);
    }
}
```

Generated mappers use `ValiantMappingMode.Auto` by default: Debug builds execute `Configure(...)` at runtime so breakpoints work, while Release builds use source-generated fast mapping. Pass `new ValiantMappingOptions { Mode = ValiantMappingMode.Fast }` or `Debuggable` to force a backend.

When dependency injection abstractions are available, the generator emits `AddValiantMappers(...)` and registers discovered mappers as concrete services, `IMapper<TSource, TTarget>` services, a convenience `IMapper` facade, `IMapperFactory`, and `ValiantMappingOptions`.

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
- `UseValidator(validator)`
- `[UseValidator<TValidator>]`

Type-specific checks are exposed only where they make sense. For example, `NotNull()` and `Null()` are available on reference and nullable value properties, length checks are available on string and collection properties, `DefinedEnum()` is available on enum properties, membership and predicate checks are available on collection properties, and dictionary key/value checks are available on dictionary properties.

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
services.AddValiantValidators(options =>
{
    options.FailureBehavior = ValiantFailureBehavior.FailFast;
});

public override void Configure(ValidationContext<CreateUser> context)
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

Valiant supports C# 15 labeled `break` and `continue` in `Configure`.

Collection and dictionary checks cover size, membership, predicates, and dictionary keys/values:

```csharp
context.Property(order => order.Items)
    .NotEmpty()
    .LengthBetween(1, 25)
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

`StaticNorth.Valiant.Validation.AspNetCore` contains ASP.NET-specific helpers so the core package can stay free of ASP.NET references.

Minimal APIs can validate endpoint models with an endpoint filter:

```csharp
using StaticNorth.Valiant.Validation.AspNetCore;

app.MapPost("/users", (CreateUser user) => Results.Ok())
    .WithValiantValidation<CreateUser>();
```

`WithValiantValidation()` uses each argument's exact declared parameter type. `[ValiantValidator]`
discovery emits the validator, its typed invocation bridge, and their registrations into the generated
`AddValiantValidators()` method, so the normal path needs no per-model registration or runtime assembly
scan. The generated method covers default validators discovered in the current compilation.
Validators are resolved from request services; arguments with the same declared type reuse that
request's resolved validator sequence while retaining handler-argument execution order. Errors return
HTTP 400 before the handler runs; warnings and informational failures do not block the handler.

This generated parameterless path supports Native AOT. Manually registered raw `IValidator<T>` services
without a generated bridge remain compatible through a JIT-only fallback. For an explicit manual
registration in Native AOT, use `WithValiantValidation<T>()`, which validates every non-null endpoint
argument assignable to `T`. MVC does not receive this Native AOT guarantee.
Both extensions add ASP.NET Core `ProducesValidationProblem(400)` endpoint metadata, so the built-in .NET OpenAPI generator can document validation problem responses without Swashbuckle or NSwag-specific hooks.

`StaticNorth.Valiant.Validation.AspNetCore.OpenApi` adds native .NET OpenAPI schema reflection for generated validation metadata:

```csharp
using StaticNorth.Valiant.Validation.AspNetCore.OpenApi;

builder.Services.AddOpenApi();
builder.Services.AddValiantValidators();
builder.Services.AddValiantOpenApi();
```

The schema transformer maps unconditional static rules to standard OpenAPI schema constraints, such as `required`, `minLength`, `maxLength`, `minimum`, `maximum`, `minItems`, and `maxItems`. It also adds an `x-valiant-rules` extension per property so codes, messages, severities, and conditional rules remain visible in `openapi.json`.

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

## Mediator integrations

`StaticNorth.Valiant.Validation.MediatR` adds a MediatR pipeline behavior that runs all registered `IValidator<TRequest>` instances before the request handler:

```csharp
using StaticNorth.Valiant.Validation.MediatR;

services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssemblyContaining<CreateUserCommand>();
    configuration.AddValiantValidation();
});
```

The package intentionally pins MediatR to `12.5.0`, the final free MediatR release, and should not be automatically updated.

`StaticNorth.Valiant.Validation.Mediator` adds the equivalent behavior for the source-generated Mediator package:

```csharp
using StaticNorth.Valiant.Validation.Mediator;

services.AddMediator(options =>
{
    options.Assemblies = [typeof(CreateUserCommand).Assembly];
});

services.AddValiantMediatorValidation();
```

For NativeAOT, configure the behavior in the `AddMediator` options delegate instead:

```csharp
using StaticNorth.Valiant.Validation.Mediator;

services.AddMediator(options =>
{
    options.Assemblies = [typeof(CreateUserCommand).Assembly];
    options.PipelineBehaviors = [typeof(ValiantValidationBehavior<,>)];
});
```

Use one Mediator registration path for the Valiant behavior. Do not also call `AddValiantMediatorValidation()` when `ValiantValidationBehavior<,>` is listed in `PipelineBehaviors`.

Both integrations validate by request/message type. To validate a command, register a normal Valiant validator for that command type:

```csharp
[ValiantValidator]
public sealed partial class CreateUserCommandValidator : ValidatorDefinition<CreateUserCommand>
{
    public override void Configure(ValidationContext<CreateUserCommand> context)
    {
        context.Property(x => x.Email).Required();
    }
}
```

If validation returns errors, the behavior throws `ValiantValidationException` and the handler is not invoked. Warnings and informational failures are preserved in the aggregate result but do not block handler execution.

## Source generation and analyzer diagnostics

The generator is incremental and attribute-driven. It discovers validators through `[ValiantValidator]` / `[ValiantValidator<T>]`, emits each validator independently, and keeps dependency-injection generation on a lightweight registration model so unrelated source edits do not force reflection-style rescans.

The generated output includes:

- `IValidator<T>` implementation for each valid validator definition.
- `IPropertyValidator<TProperty>` DI registrations for property validator definitions.
- `IValiantValidationMetadataProvider` implementation for generated metadata.
- `AddValiantValidators` when DI symbols are available in the consuming compilation.

The generated DI extension registers concrete validators, `IValidator<TModel>` services, concrete property validator definitions, `IPropertyValidator<TProperty>` services, metadata providers, and `ValiantValidationOptions`.

The source generator reports compiler diagnostics for invalid validator declarations and low-noise suggestions for suspicious rule chains:

- `VLT101` warns when a check is redundant because another check in the same chain already covers it, such as `Required().NotNull()`.
- `VLT102` warns when the same non-custom check is declared more than once in a property chain.
- `VLT103` warns when a property chain sets failure behavior more than once, because only the last `FailFast()` or `ValidateAll()` setting is used.

## Repository layout

- `validation/src/StaticNorth.Valiant.Validation` contains runtime contracts and the NuGet package metadata.
- `validation/src/StaticNorth.Valiant.Validation.AspNetCore` contains Minimal API, MVC, and problem details integrations.
- `validation/src/StaticNorth.Valiant.Validation.AspNetCore.OpenApi` contains native ASP.NET Core OpenAPI schema integration.
- `validation/src/StaticNorth.Valiant.Validation.MediatR` contains MediatR pipeline behavior integration.
- `validation/src/StaticNorth.Valiant.Validation.Mediator` contains source-generated Mediator pipeline behavior integration.
- `validation/src/StaticNorth.Valiant.Validation.SourceGeneration` contains the incremental source generator packaged as a NuGet analyzer.
- `assets/logo.png` contains the NuGet package icon used by all packable projects.
- `AGENTS.md` and `AGENT_GUIDE.md` document repository context for coding agents.
- `validation/samples/StaticNorth.Valiant.Validation.Samples` contains selectable core samples for fluent and attribute rules, generated DI, sync/async behavior, severities, composition and paths, failure handling, collections, and control flow.
- `validation/samples/StaticNorth.Valiant.Validation.AspNetCore.Samples` contains selectable ASP.NET Core samples for Minimal API filters, automatic MVC validation, problem-details conversion, and validation metadata in a real OpenAPI document.

## Local verification

Install the exact SDK declared in `global.json` before running repository
commands. SDK roll-forward is disabled because SDK-provided packages participate
in the committed NuGet lock graph.

```bash
dotnet restore StaticNorth.Valiant.sln --locked-mode
dotnet build StaticNorth.Valiant.sln --configuration Debug --no-restore
dotnet test --solution StaticNorth.Valiant.sln --configuration Debug --no-build
dotnet build StaticNorth.Valiant.sln --configuration Release --no-restore
dotnet test --solution StaticNorth.Valiant.sln --configuration Release --no-build
dotnet run --project validation/samples/StaticNorth.Valiant.Validation.Samples
dotnet run --project validation/samples/StaticNorth.Valiant.Validation.AspNetCore.Samples
dotnet msbuild StaticNorth.Valiant.Pack.proj -t:VerifyPackageConsumers -m:1 -p:Configuration=Release -p:NoBuild=true
dotnet msbuild StaticNorth.Valiant.Pack.proj -t:VerifyAotSmoke -m:1 -p:Configuration=Release -p:NoBuild=true -p:AotRuntimeIdentifier=osx-arm64
```

Repository-wide build, analyzer, package metadata, and dependency versions live in
`Directory.Build.props`, `Directory.Build.targets`, and `Directory.Packages.props`.
Every solution project commits `packages.lock.json`; after changing
`Directory.Packages.props` or the SDK version in `global.json`, regenerate the
graph with:

```bash
dotnet restore StaticNorth.Valiant.sln --force-evaluate
```

`StaticNorth.Valiant.Pack.proj` is the authoritative ten-package release manifest. Its
verification targets pack serially, inspect NuGet and symbol-package contents,
and run leaf-only applications that restore the produced packages from the
local feed. `StaticNorth.Valiant.Endpoints` is an internal contract assembly bundled inside
`StaticNorth.Valiant.Endpoints.AspNetCore`; it is not a standalone package.

## Contributing and security

Contributions target `main` through pull requests. See [CONTRIBUTING.md](CONTRIBUTING.md)
for development and verification guidance and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
for community expectations.

Do not report vulnerabilities in public issues. Follow [SECURITY.md](SECURITY.md)
to submit a private report.
