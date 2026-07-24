---
title: Quick start
description: Install Valiant.Mapping and generate your first object mapper.
---

# Quick start

## 1. Install

```bash
dotnet add package Valiant.Mapping
```

`Valiant.Mapping` contains the runtime API and delivers its source generator as an analyzer. No separate generator package is needed.

## 2. Declare the models and mapper

```csharp
using Valiant.Mapping;

public sealed class User
{
    public required string Name { get; init; }
    public int Age { get; init; }
}

public sealed record UserDto(string Name, int Age);

[ValiantMapper]
public sealed partial class UserMapper : MapperDefinition<User, UserDto>;
```

The mapper must be `partial`. `MapperDefinition<TSource, TTarget>` identifies the pair and the generator fills in the implementation.

## 3. Map

```csharp
var mapper = new UserMapper();
var dto = mapper.Map(new User { Name = "Ada", Age = 36 });
```

The mapper can also be consumed through its interface:

```csharp
IMapper<User, UserDto> mapper = new UserMapper();
UserDto dto = mapper.Map(user);
```

## 4. Register with dependency injection

When the project references Microsoft dependency-injection abstractions, Valiant generates `AddValiantMappers`:

```csharp
using Valiant.Mapping.DependencyInjection;

builder.Services.AddValiantMappers(options =>
{
    options.Mode = ValiantMappingMode.Auto;
    options.NullHandling = ValiantNullHandling.MapNulls;
});
```

Then inject the concrete mapper, `IMapper<User, UserDto>`, the non-generic `IMapper`, or `IMapperFactory`.

:::info Generated discovery
Only declarations marked with `[ValiantMapper]` enter the generator pipeline. Valiant does not scan every type in your compilation looking for mapping definitions.
:::

Next: learn what maps automatically in [Convention mapping](./guides/conventions.md).

