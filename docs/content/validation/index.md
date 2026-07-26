---
title: Validation
description: Source-generated validation with fluent or attribute rules and first-class application integrations.
---

# Validation you can step through

Valiant Validation generates direct validators from compact, strongly typed rules. Author rules with fluent C#, model attributes, native branches, nested contexts, and reusable validators; then connect them to ASP.NET Core, OpenAPI, MediatR, or source-generated Mediator.

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

## One rule model, several hosts

| Layer | Integration |
| --- | --- |
| Domain/application | Inject `IValidator<T>` and call sync or async validation APIs |
| Minimal APIs | Add `.WithValiantValidation()` to a route |
| MVC | Register Valiant model validation globally |
| OpenAPI | Project generated rule metadata into schema constraints and `x-valiant-rules` |
| Messaging | Add a MediatR or Mediator pipeline behavior |

Validation results preserve property paths, codes, messages, and severity. Errors invalidate the result; warnings and information remain available without making it invalid.

## Documentation map

- [Quick start](./quick-start.md)
- [Runnable samples](./samples.md)
- [Authoring rules](./guides/authoring-rules.md)
- [Composition and control flow](./guides/composition-control-flow.md)
- [ASP.NET Core and OpenAPI](./guides/aspnetcore-openapi.md)
- [Mediator pipelines](./guides/mediator-pipelines.md)
- [Validation check catalog](./reference/validation-checks.md)
- [Runtime and generation](./reference/runtime-generation.md)
- [Package guide](./reference/packages.md)

The [sample catalog](./samples.md) covers every selectable core and ASP.NET
Core scenario. The same behaviors are covered by sample-focused tests,
including `WebApplicationFactory` tests for every web sample.
