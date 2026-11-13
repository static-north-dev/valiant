---
title: Composition and control flow
description: Native branches, nested objects, collections, reusable root validators, and property validators.
---

# Composition and control flow

## Write normal C# branches

Validator configuration supports native `if`/`else`, `switch`, loops, and `foreach`:

```csharp
public override void Configure(ValidationContext<CreateOrder> context)
{
    if (context.Instance.RequiresApproval)
    {
        context.Property(order => order.ApproverId).Required();
    }

    switch (context.Instance.Channel)
    {
        case SalesChannel.Store:
            context.Property(order => order.StoreId).Required();
            break;
        case SalesChannel.Web:
            context.Property(order => order.SessionId).Required();
            break;
    }
}
```

In `Debuggable` mode this method executes at runtime, so ordinary breakpoints and step-through debugging work.

## Nested objects

Use `Children` to validate a nested context and retain the complete property path:

```csharp
context.Property(order => order.Customer)
    .Required()
    .Children(customer =>
    {
        customer.Property(value => value.Name).Required();
        customer.Property(value => value.Email).MaxLength(200);
    });
```

## Collections

Use `ForEach` for element rules. `context.Item` is the current item inside the nested validation context:

```csharp
context.Property(order => order.Lines)
    .MinCount(1)
    .ForEach(line =>
    {
        line.Property(item => item.Sku).Required();
        line.Property(item => item.Quantity).GreaterThan(0);
    });
```

Failures include indexed paths such as `Lines[0].Sku`.

## Reuse a root validator

Both validators must validate the same model type. Mark the reusable validator with `RegisterAsDefault = false` so it can be injected by concrete type while the outer validator remains the default `IValidator<Checkout>`.

```csharp
[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class CheckoutAddressValidator : ValidatorDefinition<Checkout>
{
    public override void Configure(ValidationContext<Checkout> context)
    {
        context.Property(value => value.ShippingAddress).Required();
    }
}

[ValiantValidator]
public sealed partial class CheckoutValidator(CheckoutAddressValidator addressValidator)
    : ValidatorDefinition<Checkout>
{
    public override void Configure(ValidationContext<Checkout> context)
    {
        context.UseValidator(addressValidator);
        context.Property(value => value.AcceptedTerms).Equal(true);
    }
}
```

## Reuse a property validator

Create a reusable `PropertyValidatorDefinition<TProperty>` and attach it to a property:

```csharp
context.Property(order => order.ShippingAddress)
    .Required()
    .UseValidator(addressPropertyValidator);
```

Property validators skip null values. Add `Required()` or `NotNull()` when absence itself is invalid. Attribute-authored models can use `[UseValidator<TValidator>]`.

## Failure behavior

Configure collection globally, then override it at validator or property-chain scope:

```csharp
services.AddValiantValidators(options =>
    options.FailureBehavior = ValiantFailureBehavior.ValidateAll);
```

Use `FailFast` to stop within the configured scope after the first failure; use `ValidateAll` to collect all applicable failures. Narrower configuration overrides the global option.
