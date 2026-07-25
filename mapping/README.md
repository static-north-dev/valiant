# Valiant Mapping

Valiant Mapping is a source-generated object mapping library for .NET. It generates direct mapping code, supports dependency injection without reflection-based assembly scanning, and is friendly to trimming and Native AOT.

## Package

- `StaticNorth.Valiant.Mapping` provides the runtime API and source generator.

## Quick Start

```csharp
using Valiant.Mapping;

[ValiantMapper]
public sealed partial class UserMapper : MapperDefinition<User, UserDto>;
```

`[ValiantMapper]` is the generator discovery hook. `MapperDefinition<TSource, TTarget>` supplies the mapping pair through `IMapperDefinition<TSource, TTarget>`.

Mapper generation is scoped to declarations carrying `[ValiantMapper]`. Calls to `IMapperFactory` and the non-generic `IMapper` facade are checked by a separate analyzer, which discovers mapper pairs directly from attributed local declarations and referenced Valiant mapper registrations. This keeps ordinary invocation syntax out of the incremental generation graph while preserving missing or ambiguous mapper diagnostics.

The generator emits a direct `Map(User source)` implementation and adds `IMapper<User, UserDto>` to the generated partial type. Public source properties and fields are mapped to public settable target properties and fields by name.

Constructor parameters are mapped first, which supports immutable targets and records:

```csharp
public sealed record UserDto(string Name, int Age);
```

## Explicit Mapping

Mapper definitions can override `Configure(...)` for explicit member mappings:

```csharp
public sealed partial class UserMapper : MapperDefinition<User, UserDto>
{
    public override void Configure(MappingContext<User, UserDto> context)
    {
        context.Map(source => source.EmailAddress, target => target.Email)
            .When(source => source.IncludeEmail);
    }
}
```

When `Configure(...)` is present, convention mapping is disabled so mapping ownership stays explicit. Unmapped or incompatible targets are reported through the normal mapping diagnostics.

## Reuse

Mappers can reuse other generated mappers through constructor injection:

```csharp
public sealed partial class UserMapper(AddressMapper addressMapper) : MapperDefinition<User, UserDto>
{
    public override void Configure(MappingContext<User, UserDto> context)
    {
        context.Map(source => source.Address, target => target.Address)
            .UseMapper(addressMapper);
    }
}
```

## Dependency Injection

When dependency injection abstractions are available, the generator emits `AddValiantMappers(...)` and registers discovered mappers as concrete services, `IMapper<TSource, TTarget>` services, a convenience `IMapper` facade, `IMapperFactory`, and `ValiantMappingOptions`.

```csharp
services.AddValiantMappers(options =>
{
    options.Mode = ValiantMappingMode.Auto;
    options.NullHandling = ValiantNullHandling.MapNulls;
});

var mapper = provider.GetRequiredService<IMapper>();
UserDto dto = mapper.Map<UserDto>(user);

var factory = provider.GetRequiredService<IMapperFactory>();
var typedMapper = factory.CreateMapper<User, UserDto>();
```

Runnable, selectable examples live in
`mapping/samples/StaticNorth.Valiant.Mapping.Samples`. Each feature folder contains its
models, mapper, and a documented `Sample` entry point.
