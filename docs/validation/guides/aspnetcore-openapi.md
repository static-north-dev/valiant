---
title: ASP.NET Core and OpenAPI
description: Validate Minimal API and MVC inputs, produce problem details, and publish rule metadata to OpenAPI.
---

# ASP.NET Core and OpenAPI

Install the integrations you use:

```bash
dotnet add package Valiant.Validation.AspNetCore
dotnet add package Valiant.Validation.AspNetCore.OpenApi
```

## Minimal APIs

Register validators and attach validation to a route:

```csharp
builder.Services.AddValiantValidators();

app.MapPost("/users", (CreateUser request) => Results.Created())
    .WithValiantValidation();
```

The non-generic overload validates every eligible handler argument with a registered validator. Use the typed overload to select one request type explicitly:

```csharp
app.MapPost("/users", (CreateUser request, CancellationToken cancellationToken) =>
        Results.Created())
    .WithValiantValidation<CreateUser>();
```

Failures return HTTP validation problem details before the handler runs.

## MVC

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

```csharp
builder.Services.AddOpenApi();
builder.Services.AddValiantOpenApi();
```

Or attach the transformer to a specific OpenAPI document:

```csharp
builder.Services.AddOpenApi(options =>
    options.AddValiantValidationRules());
```

Valiant projects generated metadata into native constraints where possible—such as required, length, count, and comparison bounds—and preserves richer details in `x-valiant-rules` extensions.

:::info Package references
Each integration package forwards the core Valiant runtime and generator assets. Add both ASP.NET Core packages when you need request validation and OpenAPI; do not add a redundant direct `Valiant.Validation` reference.
:::

The [Validation sample catalog](../samples.md#aspnet-core-validation) links the
Minimal API, MVC, problem-details, and OpenAPI startup implementations.
