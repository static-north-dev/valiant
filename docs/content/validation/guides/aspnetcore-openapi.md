---
title: ASP.NET Core and OpenAPI
description: Validate Minimal API and MVC inputs, produce problem details, and publish rule metadata to OpenAPI.
---

# ASP.NET Core and OpenAPI

Install the integrations you use:

```bash
dotnet add package StaticNorth.Valiant.Validation.AspNetCore
dotnet add package StaticNorth.Valiant.Validation.AspNetCore.OpenApi
```

## Minimal APIs

Register validators and attach validation to a route:

```csharp
builder.Services.AddValiantValidators();

app.MapPost("/users", (CreateUser request) => Results.Created())
    .WithValiantValidation();
```

The non-generic overload uses each non-null argument's exact declared parameter type.
`[ValiantValidator]` discovery emits the validator, its typed invocation bridge, and their registrations
into the generated `AddValiantValidators()` method. The normal path therefore needs no per-model
registration or runtime assembly scan. The generated method covers default validators discovered in
the current compilation. Arguments with the same declared type reuse the validators resolved for that
request while retaining handler-argument execution order.

Use the typed overload to resolve `IValidator<T>` and validate every non-null handler argument
assignable to `T`:

```csharp
app.MapPost("/users", (CreateUser request, CancellationToken cancellationToken) =>
        Results.Created())
    .WithValiantValidation<CreateUser>();
```

The generated parameterless path supports Native AOT. Manually registered raw `IValidator<T>` services
without a generated bridge remain compatible through a JIT-only fallback. For an explicit manual
registration in Native AOT, use the typed overload. MVC does not receive this Native AOT guarantee.

Errors return HTTP validation problem details before the handler runs. Warnings and informational
failures do not block the handler.

## MVC

MVC model validation is synchronous. For validators with async rules, invoke `ValidateAsync` explicitly from an async action in place of the automatic Valiant MVC integration for that model.

Register the global model-validation filter directly or through MVC builders:

```csharp
builder.Services.AddValiantValidators();
builder.Services.AddValiantMvcValidation();

// Equivalent integration point:
builder.Services.AddControllers().AddValiantValidation();
```

## Problem details helpers

Use `ValiantProblemDetails` when your endpoint or controller validates manually:

```csharp
var result = await validator.ValidateAsync(request, cancellationToken);
if (!result.IsValid)
{
    return Results.ValidationProblem(result.ToValidationProblemDictionary());
}
```

Helpers are available for Minimal API `HttpValidationProblemDetails`, MVC `ValidationProblemDetails`, and property-to-message dictionaries.

## OpenAPI

The OpenAPI integration uses OpenAPI.NET 2.x on .NET 10 and OpenAPI.NET 3.x on .NET 11.

On .NET 11, ASP.NET Core describes C# 15 union JSON bodies with `anyOf`. The Valiant schema transformer includes validation rules on case models with generated validation metadata. To validate the request at runtime, register a validator for the union parameter type and delegate to the relevant case validator as needed.

```csharp
builder.Services.AddOpenApi();
builder.Services.AddValiantOpenApi();
```

Or attach the transformer to a specific OpenAPI document:

```csharp
builder.Services.AddOpenApi(options =>
    options.AddValiantValidationRules());
```

Valiant projects unconditional rules with a known blocking severity into native constraints such as required, length, count, and comparison bounds. Rules with advisory, dynamic, or conditional severity remain available in the `x-valiant-rules` extension.

:::info Package references
Each integration package forwards the core Valiant runtime and generator assets. Add both ASP.NET Core packages when you need request validation and OpenAPI; do not add a redundant direct `StaticNorth.Valiant.Validation` reference.
:::

The [Validation sample catalog](../samples.md#aspnet-core-validation) links the
Minimal API, MVC, problem-details, and OpenAPI startup implementations.
