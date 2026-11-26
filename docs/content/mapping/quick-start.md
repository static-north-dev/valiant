---
title: Quick start
description: Install StaticNorth.Valiant.Mapping and generate your first object mapper.
---

# Quick start

## 1. Create a project and install Valiant

Use the **.NET 10.0.400 SDK or newer** with this guide. The project can target .NET 8. Run `dotnet --version` from the project directory to check the selected SDK. A `CS9057` warning indicates that the selected compiler is too old to load the generator.

Generated code requires **C# 8.0 or later**. Set `<LangVersion>8.0</LangVersion>` explicitly in projects that default to an older language version, including .NET Standard 2.0 projects.

```bash
dotnet new console -n MappingDemo -f net8.0
cd MappingDemo
dotnet add package StaticNorth.Valiant.Mapping
```

`StaticNorth.Valiant.Mapping` contains the runtime API and delivers its source generator as an analyzer. No separate generator package is needed.

The install command selects the latest stable package. Add `--version` to select a specific release or prerelease that matches the guide you are following.

## 2. Declare the models and mapper

```csharp title="UserMapper.cs"
using StaticNorth.Valiant.Mapping;

public sealed class User
{
    public required string Name { get; init; }
    public int Age { get; init; }
}

public sealed record UserDto(string? Name, int Age);

[ValiantMapper]
public sealed partial class UserMapper : MapperDefinition<User, UserDto>;
```

The mapper must be `partial`. `MapperDefinition<TSource, TTarget>` identifies the pair and the generator fills in the implementation.

## 3. Map

Replace the contents of `Program.cs`. The generated mapper works directly or through its typed interface.

```csharp title="Program.cs"
using StaticNorth.Valiant.Mapping;

var user = new User { Name = "Ada", Age = 36 };
IMapper<User, UserDto> mapper = new UserMapper();
UserDto dto = mapper.Map(user);
Console.WriteLine($"{dto.Name}, age {dto.Age}");
```

Run `dotnet run`. The output is `Ada, age 36`.

## 4. Register with dependency injection

When the project references Microsoft dependency-injection abstractions, Valiant generates `AddValiantMappers`:

```csharp
using StaticNorth.Valiant.Mapping;

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

