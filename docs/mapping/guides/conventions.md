---
title: Convention mapping
description: Map compatible members, records, constructors, nested shapes, collections, and enums by convention.
---

# Convention mapping

With no `Configure` override, Valiant maps public readable source fields and properties to public writable target fields, properties, or constructor parameters.

## Matching members

Names are matched case-insensitively. Compatible values are assigned directly:

```csharp
public sealed class Order
{
    public Guid Id { get; init; }
    public decimal Total { get; init; }
}

public sealed class OrderDto
{
    public Guid Id { get; init; }
    public decimal Total { get; init; }
}

[ValiantMapper]
public sealed partial class OrderMapper : MapperDefinition<Order, OrderDto>;
```

## Records and constructors

Constructor parameters are mapped before writable members, so immutable targets work naturally:

```csharp
public sealed record OrderDto(Guid Id, decimal Total);
```

Valiant chooses a usable public constructor and reports a compile-time diagnostic when the target cannot be constructed or a required member cannot be supplied.

## Flattening and unflattening

Concatenated member paths can bridge nested and flat shapes:

```csharp
public sealed class User
{
    public required Address Address { get; init; }
}

public sealed class Address
{
    public required string City { get; init; }
}

public sealed class UserDto
{
    public required string AddressCity { get; init; }
}
```

The reverse shape is supported when a flat source member can populate a nested target path. Prefer explicit mapping when concatenated names could be ambiguous.

## Enums

Enums with compatible values map between enum types. Treat cross-domain enum contracts carefully: an explicit transformation is clearer when numeric values can drift independently.

## Nested objects and collections

Register a mapper for the element or nested pair, then reuse it from the parent mapper:

```csharp
[ValiantMapper]
public sealed partial class LineMapper : MapperDefinition<OrderLine, OrderLineDto>;

[ValiantMapper]
public sealed partial class OrderMapper(LineMapper lineMapper)
    : MapperDefinition<Order, OrderDto>
{
    public override void Configure(MappingContext<Order, OrderDto> context)
    {
        context.Map(source => source.Lines, target => target.Lines)
            .UseMapper(lineMapper);
    }
}
```

Valiant can apply the reusable mapper to compatible nested values and collection elements. See [Composition and dependency injection](./composition-dependency-injection.md) for registration details.

:::warning Explicit mode changes ownership
Declaring `Configure` disables convention mapping for that mapper. Map or ignore every target member intentionally; diagnostics will identify missing or incompatible targets.
:::

