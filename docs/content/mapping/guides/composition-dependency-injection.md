---
title: Composition and dependency injection
description: Reuse generated mappers and choose between typed, facade, and factory APIs.
---

# Composition and dependency injection

## Reuse another mapper

Constructor-inject the generated mapper and attach it to a member mapping:

```csharp
[ValiantMapper]
public sealed partial class AddressMapper
    : MapperDefinition<Address, AddressDto>;

[ValiantMapper]
public sealed partial class UserMapper(AddressMapper addressMapper)
    : MapperDefinition<User, UserDto>
{
    public override void Configure(MappingContext<User, UserDto> context)
    {
        context.Map(source => source.Address, target => target.Address)
            .UseMapper(addressMapper);
    }
}
```

`UseMapper` can be called on the context or in a fluent mapping chain. It makes nested object and collection-element conversion explicit and keeps each pair independently testable.

## Generated registration

```csharp
using StaticNorth.Valiant.Mapping;

builder.Services.AddValiantMappers();
```

Valiant registers each mapper as its concrete type and as `IMapper<TSource, TTarget>`. It also registers `IMapper` and `IMapperFactory`. Mapper registrations are transient; the facade, factory, and options are singleton services.

## Choose an API

### Typed mapper

Use the typed interface when the pair is known at compile time:

```csharp
public sealed class UserHandler(IMapper<User, UserDto> mapper)
{
    public UserDto Handle(User user) => mapper.Map(user);
}
```

### Non-generic facade

Use `IMapper` when the source arrives as `object` but the target is known or supplied dynamically:

```csharp
UserDto dto = mapper.Map<UserDto>(source);

if (mapper.TryMap(source, typeof(UserDto), out var value))
{
    // value is the mapped object.
}
```

### Factory

Use `IMapperFactory` when you need the mapper instance itself:

```csharp
var typed = factory.CreateMapper<User, UserDto>();

if (factory.TryCreateMapper(typeof(User), typeof(UserDto), out var mapper))
{
    // mapper is the registered IMapper<User, UserDto> instance.
}
```

Calls to `CreateMapper`/`Map` for a statically knowable unregistered pair produce analyzer diagnostics. Use the `Try...` APIs when absence is expected.

