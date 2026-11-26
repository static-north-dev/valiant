---
title: Quick start
description: Install StaticNorth.Valiant.Validation, generate a validator, and consume its result.
---

# Quick start

## 1. Create a project and install Valiant

Use the **.NET 10.0.400 SDK or newer** with this guide. The project can target .NET 8. Run `dotnet --version` from the project directory to check the selected SDK. A `CS9057` warning indicates that the selected compiler is too old to load the generator.

Generated code requires **C# 9.0 or later**. Set `<LangVersion>9.0</LangVersion>` explicitly in projects that default to an older language version, including .NET Standard 2.0 projects.

```bash
dotnet new console -n ValidationDemo -f net8.0
cd ValidationDemo
dotnet add package StaticNorth.Valiant.Validation
```

For an application integration, install the most specific package instead. For example, `StaticNorth.Valiant.Validation.AspNetCore` carries the core runtime, build, and analyzer assets needed to generate validators.

The install command selects the latest stable package. Add `--version` to select a specific release or prerelease that matches the guide you are following.

## 2. Declare a validator

```csharp title="RegisterUserValidator.cs"
using StaticNorth.Valiant.Validation;

public sealed record RegisterUser(string? Name, int Age);

[ValiantValidator]
public sealed partial class RegisterUserValidator
    : ValidatorDefinition<RegisterUser>
{
    public override void Configure(ValidationContext<RegisterUser> context)
    {
        context.Property(request => request.Name)
            .Required("NameRequired", "Enter a name.")
            .MaxLength(64);

        context.Property(request => request.Age)
            .GreaterThanOrEqualTo(18);
    }
}
```

The declaration must be `partial` and marked with `[ValiantValidator]`. The generator emits `IValidator<RegisterUser>` directly on the class.

## 3. Validate

Replace the contents of `Program.cs`:

```csharp title="Program.cs"
using StaticNorth.Valiant.Validation;

IValidator<RegisterUser> validator = new RegisterUserValidator();
ValidationResult result = validator.Validate(new RegisterUser(null, 16));

if (!result.IsValid)
{
    foreach (var failure in result.Errors)
    {
        Console.WriteLine($"{failure.PropertyName}: {failure.ErrorMessage}");
    }
}
```

Run `dotnet run`. The example prints an error for `Name` and an error for `Age`.

Use `ValidateAsync` for validators that contain async rules. Validators also expose `IsValid` and `IsValidAsync`. Call `ThrowIfFailed()` on a result to raise a validation exception.

## 4. Register generated validators in an application

With Microsoft dependency injection available, register validators from the project that declares them.

From a separate host, call each library's generated registration explicitly. For a library named `Orders`, the entry point is `StaticNorth.Valiant.Generated.Assembly_Orders.Validators.AddValiantValidators(services)`. See [generated registration namespaces](../mapping/guides/composition-dependency-injection.md) for assembly names with punctuation.

If a validator has no public constructor with all parameters passed by value, register its factory as an `IValidator<T>` in DI.

```csharp
using StaticNorth.Valiant.Validation;
using StaticNorth.Valiant.Validation.DependencyInjection;

builder.Services.AddValiantValidators(options =>
{
    options.Mode = ValiantValidationMode.Auto;
    options.FailureBehavior = ValiantFailureBehavior.ValidateAll;
    options.Lifetime = ValiantValidatorLifetime.Scoped;
});
```

Inject `IValidator<RegisterUser>` wherever the request is handled.

:::tip Async rule safety
Call `ValidateAsync` when a validator or reused property validator may perform asynchronous work. The synchronous API does not silently block on async validation.
:::
