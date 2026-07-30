# Mapping benchmarks

These benchmarks measure Valiant's generated `Fast` mapping path against
Mapperly:

- `FlatMappingBenchmarks` maps eight scalar properties.
- `NestedCollectionMappingBenchmarks` maps a parent, a nested object, and
  collections containing 0, 10, or 100 elements.
- `ArrayCollectionMappingBenchmarks` maps exact-size arrays.
- `SetCollectionMappingBenchmarks` maps directly into `HashSet<T>`.
- `EnumerableCollectionMappingBenchmarks` maps an `IEnumerable<T>` whose runtime
  value is an array, covering runtime exact-sizing.

`GlobalSetup` deep-compares every mapped scalar, nested object, and collection
element before timing begins. Mapper construction and source creation are
excluded. Destination and collection allocation are included.

## Run

Run the complete benchmark in Release mode:

```bash
dotnet run --project mapping/benchmarks/StaticNorth.Valiant.Mapping.Benchmarks --configuration Release -- --filter '*'
```

Use BenchmarkDotNet's dry job only to verify that every case can execute:

```bash
dotnet run --project mapping/benchmarks/StaticNorth.Valiant.Mapping.Benchmarks --configuration Release -- --job Dry --filter '*'
```

The dry job is a smoke check, not performance evidence.

## Regression workflow

Run the same command on the before and after commits on the same idle machine,
using separate artifact directories:

```bash
dotnet run --project mapping/benchmarks/StaticNorth.Valiant.Mapping.Benchmarks --configuration Release -- --filter '*' --artifacts artifacts/mapping-before
dotnet run --project mapping/benchmarks/StaticNorth.Valiant.Mapping.Benchmarks --configuration Release -- --filter '*' --artifacts artifacts/mapping-after
```

Compare Valiant's absolute mean and allocated bytes between commits. Also compare
the Valiant-to-Mapperly ratio for each case; Mapperly is a pinned relative
control, not a replacement for before/after Valiant measurements.

Accept a mapping optimization only when Valiant is within 5% of Mapperly for the
same shape, existing Valiant flat/list cases do not regress by more than 5%, and
allocation is equal or lower.
