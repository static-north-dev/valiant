---
title: Mediator pipelines
description: Validate requests automatically in MediatR and source-generated Mediator pipelines.
---

# Mediator pipelines

## MediatR

Install `StaticNorth.Valiant.Validation.MediatR`, register generated validators, and add the behavior through MediatR configuration:

```csharp
builder.Services.AddValiantValidators();

builder.Services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssemblyContaining<CreateUserCommand>();
    configuration.AddValiantValidation();
});
```

The package targets MediatR 12.5.0, the final free MediatR release.

## Source-generated Mediator

Install `StaticNorth.Valiant.Validation.Mediator`:

```csharp
builder.Services.AddValiantValidators();
builder.Services.AddMediator(options =>
{
    options.Assemblies = [typeof(CreateUserCommand).Assembly];
});
builder.Services.AddValiantMediatorValidation();
```

For Native AOT, list the behavior in the source-generated pipeline:

```csharp
builder.Services.AddMediator(options =>
{
    options.Assemblies = [typeof(CreateUserCommand).Assembly];
    options.PipelineBehaviors = [typeof(ValiantValidationBehavior<,>)];
});
```

:::warning Choose one Mediator registration path
Do not call `AddValiantMediatorValidation()` when `ValiantValidationBehavior<,>` is already present in `PipelineBehaviors`.
:::

Both integrations resolve all registered `IValidator<TRequest>` instances, run them before the next handler, and surface validation failure instead of invoking the handler.

