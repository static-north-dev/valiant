---
title: Authoring rules
description: Fluent rules, attribute rules, custom predicates, severity, and failure metadata.
---

# Authoring rules

## Fluent definitions

Inherit `ValidatorDefinition<T>` and override `Configure`:

```csharp
[ValiantValidator]
public sealed partial class ProductValidator : ValidatorDefinition<Product>
{
    public override void Configure(ValidationContext<Product> context)
    {
        context.Property(product => product.Name)
            .Required()
            .LengthBetween(3, 80);

        context.Property(product => product.Price)
            .GreaterThan(0m);
    }
}
```

The instance method also works naturally with constructor-injected dependencies and reusable validators.

## Attribute definitions

Derive from `ValidatorDefinition<T>` to read Valiant validation attributes from the model:

```csharp
[ValiantValidator]
public sealed partial class ProductValidator : ValidatorDefinition<Product>;

public sealed class Product
{
    [Required("NameRequired", "Enter a product name.")]
    [LengthBetween(3, 80)]
    public string? Name { get; init; }

    [GreaterThan(0)]
    public decimal Price { get; init; }
}
```

These are attributes from `StaticNorth.Valiant.Validation`, not Data Annotations. Fluent and attribute rules use the same generated checks and metadata.

## Custom predicates

Use `Satisfies` for a rule that is clearer as code:

```csharp
context.Property(order => order.Reference)
    .Satisfies(
        value => value is null || value.StartsWith("ORD-", StringComparison.Ordinal),
        code: "ReferenceFormat",
        message: "Reference must start with ORD-.");
```

Async predicate overloads are available for work that accepts a cancellation token.

## Conditions

Apply a condition to a property chain:

```csharp
context.Property(request => request.InviteCode)
    .Required()
    .When(request => request.RequiresInvite);
```

For larger branches, use normal C# control flow. See [Composition and control flow](./composition-control-flow.md).

## Codes, messages, and severity

Every built-in check accepts optional `code`, `message`, and `severity` arguments:

```csharp
context.Property(user => user.DisplayName)
    .MaxLength(
        40,
        code: "DisplayNameLong",
        message: "Display name is longer than 40 characters.",
        severity: ValiantSeverity.Warning);
```

`ValidationResult.Errors` determine `IsValid`. `Warnings` and `Infos` are preserved separately for callers that want non-blocking feedback.
