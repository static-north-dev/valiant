# Valiant Mapping

Valiant Mapping is a source-generated object mapping library for .NET. It generates direct mapping code, supports dependency injection without reflection-based assembly scanning, and is friendly to trimming and Native AOT.

## Package

- `StaticNorth.Valiant.Mapping` provides the runtime API and source generator.

## Quick Start

```csharp
using StaticNorth.Valiant.Mapping;

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

Mapping options are startup configuration. Each generated mapper snapshots
`Mode` and `NullHandling` when it is constructed, so mutating the shared options
instance later does not change an existing mapper's behavior. Construct or
resolve a new mapper after changing options in tests or dynamic host setup.

## Collection Mapping

Generated `Fast` mappings write directly into the destination collection shape:

- arrays use exact-size indexed loops when the source is an array;
- `HashSet<T>` targets are populated directly and pre-sized when the target
  framework exposes the capacity constructor;
- unknown `IEnumerable<T>` sources use runtime array/count checks when possible,
  then fall back to a single-pass growable buffer.

These paths do not use LINQ projection intermediates and preserve one-shot
enumerable behavior.

Runnable, selectable examples live in
`mapping/samples/StaticNorth.Valiant.Mapping.Samples`. Each feature folder contains its
models, mapper, and a documented `Sample` entry point.

## Performance

`StaticNorth.Valiant.Mapping` benchmarks generated `Fast` mappings against the
pinned Mapperly 4.3.1 control. The suite covers flat objects, nested lists,
arrays, runtime-array-backed `IEnumerable<T>` values, and `HashSet<T>` targets.

This local reference snapshot was captured on 2026-07-30 with BenchmarkDotNet
0.15.8 and .NET 10.0.10 Arm64. Lower ratios are faster.

| Covered operation | Valiant | Mapperly | Valiant / Mapperly |
| --- | ---: | ---: | ---: |
| Flat object | 7.350 ns | 7.480 ns | 0.98 |
| List with 100 elements | 698.61 ns | 700.81 ns | 1.00 |
| Array with 100 elements | 632.386 ns | 636.567 ns | 0.99 |
| Array-backed `IEnumerable<T>` with 100 elements | 638.460 ns | 779.022 ns | 0.82 |
| `HashSet<T>` with 100 elements | 2.583 μs | 3.779 μs | 0.68 |

The covered flat, list, and array workloads are at or near parity with the
pinned control; the covered runtime-sized enumerable and `HashSet<T>` workloads
are faster. Results are workload- and machine-dependent, not a performance
guarantee. Refresh or remove this snapshot when the benchmark workload, pinned
control, target runtime, or performance-sensitive implementation changes.

## Benchmarks

The standalone
[`StaticNorth.Valiant.Mapping.Benchmarks`](benchmarks/StaticNorth.Valiant.Mapping.Benchmarks)
project measures generated `Fast` flat and nested collection mapping, with
Mapperly as a pinned relative control. Its README documents correctness checks,
the complete benchmark matrix, and the repeatable before/after workflow:

```bash
dotnet run --project mapping/benchmarks/StaticNorth.Valiant.Mapping.Benchmarks --configuration Release -- --filter '*'
```
