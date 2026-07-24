---
title: Quick start
description: Install Valiant.Validation, generate a validator, and consume its result.
---

# Quick start

## 1. Install

```bash
dotnet add package Valiant.Validation
```

For an application integration, install the most specific package instead. For example, `Valiant.Validation.AspNetCore` carries the core runtime, build, and analyzer assets needed to generate validators.

## 2. Declare a validator

```csharp
using Valiant.Validation;

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

```csharp
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

Use `ValidateAsync` for validators that contain async rules. Convenience APIs include `IsValid`, `IsValidAsync`, and `ThrowIfFailed` on the result.

## 4. Register generated validators

```csharp
using Valiant.Validation.DependencyInjection;

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
