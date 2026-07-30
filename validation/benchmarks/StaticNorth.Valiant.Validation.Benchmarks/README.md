# Validation benchmarks

These benchmarks measure Valiant's generated `Fast` validation path against
Validly. Both implementations validate five independent properties with one
equivalent rule per property.

- `ValidationBenchmarks` measures full diagnostic result creation.
- `ValidationStatusBenchmarks` measures status-only validation.
- `ValidationResultConstructionBenchmarks` isolates result construction for
  homogeneous and mixed severities.
- `ValidationResultAccessBenchmarks` measures indexed and enumerated severity
  partition access.

| Scenario | Expected failures |
| --- | --- |
| `Valid` | none |
| `OneFailure` | `DisplayName` |
| `AllFailures` | `Age`, `CountryCode`, `DisplayName`, `Nickname`, `Username` |

`GlobalSetup` validates the result status, exact failure count, and failed
property names before timing begins. Validator construction and input creation
are excluded. Result creation is measured, and Validly result disposal is
included because its result is pool-backed.

## Run

Run the complete benchmark in Release mode:

```bash
dotnet run --project validation/benchmarks/StaticNorth.Valiant.Validation.Benchmarks --configuration Release -- --filter '*'
```

Use BenchmarkDotNet's dry job only to verify that every case can execute:

```bash
dotnet run --project validation/benchmarks/StaticNorth.Valiant.Validation.Benchmarks --configuration Release -- --job Dry --filter '*'
```

The dry job is a smoke check, not performance evidence.

## Regression workflow

Run the same command on the before and after commits on the same idle machine,
using separate artifact directories:

```bash
dotnet run --project validation/benchmarks/StaticNorth.Valiant.Validation.Benchmarks --configuration Release -- --filter '*' --artifacts artifacts/validation-before
dotnet run --project validation/benchmarks/StaticNorth.Valiant.Validation.Benchmarks --configuration Release -- --filter '*' --artifacts artifacts/validation-after
```

Compare Valiant's absolute mean and allocated bytes between commits. Also compare
the Valiant-to-Validly ratio for each scenario; Validly is a pinned relative
control, not a replacement for before/after Valiant measurements.

For result representation changes, mixed-severity construction must not regress
by more than 5%, partition access must remain allocation-free, and allocated
bytes must decrease. For status-only validation, generated Valiant and Validly
must return the same status before measurement.
