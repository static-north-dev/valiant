# Valiant full benchmark report

This is the baseline snapshot before the Validation failure-path optimizations. See the [current complete Validation and Mapping rerun](2026-09-26-full-benchmarks-current.md) for all 47 current cases, the [current allocation measurements and verification](2026-09-26-validation-allocation-optimization.md) for failure-path details, or the [initial owned-result optimization](2026-09-26-validation-failure-optimization.md) for the intermediate measurements.

Run on 2026-09-26 with BenchmarkDotNet 0.15.8, .NET SDK 10.0.400, .NET runtime 10.0.11, macOS 27.0, Arm64 RyuJIT. Both projects ran every discovered benchmark using the `ShortRun` job in the in-process toolchain: 3 warmups and 3 measured iterations, one launch. Results are directional microbenchmarks, not stable release-grade baselines; the in-process runner was used because the sandbox could not complete the default child-process launch.

## Coverage

| Module | Benchmark classes | Cases | Status |
|---|---:|---:|---|
| Validation | 4 | 17 | Complete |
| Mapping | 5 | 26 | Complete |
| Settings (Configuration) | 0 | 0 | No benchmark project exists |
| Endpoints | 0 | 0 | No benchmark project exists |

All runtime benchmark cases are grouped below. `Allocated` is managed allocation per operation; `-` means no managed allocation was reported. Ratios compare to each class's declared baseline where one exists.

## Validation

### ValidationBenchmarks — validation execution against Validly

| Method | Scenario | Mean | Allocated |
|---|---|---:|---:|
| ValiantFast | Valid | 3.756 ns | — |
| Validly | Valid | 56.393 ns | — |
| ValiantFast | OneFailure | 45.220 ns | 216 B |
| Validly | OneFailure | 57.937 ns | — |
| Validly | AllFailures | 70.618 ns | — |
| ValiantFast | AllFailures | 110.567 ns | 344 B |

### ValidationResultAccessBenchmarks — result status and partition access

| Method | Mean | Allocated |
|---|---:|---:|
| EnumeratePartitions | 8.928 ns | — |
| IndexedPartitionAccess | 9.242 ns | — |

### ValidationResultConstructionBenchmarks — result creation

| Scenario | Mean | Allocated |
|---|---:|---:|
| OneError | 19.99 ns | 128 B |
| FiveErrors | 72.89 ns | 256 B |
| MixedSeverities | 91.37 ns | 424 B |

### ValidationStatusBenchmarks — generated status check against Validly

| Method | Scenario | Mean | Allocated |
|---|---|---:|---:|
| ValiantGeneratedStatus | Valid | 2.031 ns | — |
| ValidlyStatus | Valid | 56.197 ns | — |
| ValiantGeneratedStatus | OneFailure | 1.826 ns | — |
| ValidlyStatus | OneFailure | 54.866 ns | — |
| ValiantGeneratedStatus | AllFailures | 5.580 ns | — |
| ValidlyStatus | AllFailures | 74.111 ns | — |

## Mapping

### ArrayCollectionMappingBenchmarks

| Method | Collection size | Mean | Allocated |
|---|---:|---:|---:|
| ValiantArray | 0 | 8.025 ns | 48 B |
| MapperlyArray | 0 | 8.320 ns | 48 B |
| ValiantArray | 10 | 78.471 ns | 608 B |
| MapperlyArray | 10 | 81.334 ns | 608 B |
| ValiantArray | 100 | 685.639 ns | 5,648 B |
| MapperlyArray | 100 | 725.781 ns | 5,648 B |

### EnumerableCollectionMappingBenchmarks

| Method | Collection size | Mean | Allocated |
|---|---:|---:|---:|
| ValiantEnumerable | 0 | 8.640 ns | 48 B |
| MapperlyEnumerable | 0 | 22.579 ns | 88 B |
| ValiantEnumerable | 10 | 82.810 ns | 608 B |
| MapperlyEnumerable | 10 | 115.745 ns | 720 B |
| ValiantEnumerable | 100 | 715.511 ns | 5,648 B |
| MapperlyEnumerable | 100 | 831.337 ns | 5,760 B |

### FlatMappingBenchmarks

| Method | Mean | Allocated |
|---|---:|---:|
| ValiantFast | 8.100 ns | 96 B |
| Mapperly | 8.271 ns | 96 B |

### NestedCollectionMappingBenchmarks

| Method | Collection size | Mean | Allocated |
|---|---:|---:|---:|
| ValiantFast | 0 | 27.57 ns | 216 B |
| Mapperly | 0 | 28.30 ns | 216 B |
| ValiantFast | 10 | 104.57 ns | 800 B |
| Mapperly | 10 | 180.81 ns | 840 B |
| ValiantFast | 100 | 741.69 ns | 5,840 B |
| Mapperly | 100 | 1,364.64 ns | 5,880 B |

### SetCollectionMappingBenchmarks

| Method | Collection size | Mean | Allocated |
|---|---:|---:|---:|
| ValiantSet | 0 | 16.34 ns | 152 B |
| MapperlySet | 0 | 56.21 ns | 272 B |
| ValiantSet | 10 | 323.43 ns | 904 B |
| MapperlySet | 10 | 570.36 ns | 1,488 B |
| ValiantSet | 100 | 3,027.31 ns | 7,144 B |
| MapperlySet | 100 | 4,827.10 ns | 12,424 B |

## Findings

The existing Mapping cases are at or below their Mapperly baselines in these runs; array mapping uses the same allocation, and enumerable, nested, and set mapping allocate less in the measured configurations. Validation's generated status checks are much faster than the Validly status checks in the benchmark, while full validation with all failures allocates 344 B and is slower than the comparison implementation in this short run. The ValidationResult access paths are effectively tied at this precision. ShortRun noise is especially visible in several wide confidence intervals, so small differences should not be treated as regressions or wins without a longer isolated run.

These benchmark projects measure runtime validation and mapping. They do not benchmark source-generator discovery or incremental IDE change detection. The separate generator IDE test results, including edit/no-op timings and the caveat that absolute IDE CPU/RSS has not been profiled, are documented in [the performance, security, and IDE audit](2026-09-26-performance-security-ide.md). Settings and Endpoints currently have correctness/IDE tests but no runtime BenchmarkDotNet suites; their performance remains unmeasured by a formal benchmark.

### Validation failure-path allocation

The one-failure case is approximately 22% faster for Valiant in this run, but its 216 B allocation is higher than Validly's reported zero. The all-failures case is approximately 57% slower and allocates 344 B. These are real limitations of the current full-result path.

Inspection of both generated validators and the installed Validly 1.3.0 package confirms different result ownership: Validly obtains pooled results and buffers, and the benchmark's `using` returns the result to the pool on each call. Valiant returns an owned immutable result without disposal, allocating a `ValidationResultBuilder`, a failure array, a result object, and a read-only wrapper. Both generated validators execute all five rules in these benchmark cases. The results therefore compare the intended full-result APIs, including their different lifetimes.

The corresponding direct construction benchmarks allocate 128 B for one error and 256 B for five errors, each 88 B less than generated full validation. The generated code uses literal failure strings, and its additional allocation is the heap-allocated builder. Removing that builder allocation from generated execution is a concrete optimization target while preserving the existing owned-result API. Achieving Validly's steady-state zero allocation with arbitrary retained results would require a separate result-lifetime design. The memory diagnoser's zero allocation excludes memory retained by pools, and these measurements do not isolate the contribution of each component to execution time.

## Raw BenchmarkDotNet output

- Validation: [ValidationBenchmarks](../../artifacts/audit/benchmarks/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationBenchmarks-report-github.md), [ValidationResultAccessBenchmarks](../../artifacts/audit/benchmarks/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationResultAccessBenchmarks-report-github.md), [ValidationResultConstructionBenchmarks](../../artifacts/audit/benchmarks/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationResultConstructionBenchmarks-report-github.md), [ValidationStatusBenchmarks](../../artifacts/audit/benchmarks/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationStatusBenchmarks-report-github.md).
- Mapping: [ArrayCollectionMappingBenchmarks](../../artifacts/audit/benchmarks/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.ArrayCollectionMappingBenchmarks-report-github.md), [EnumerableCollectionMappingBenchmarks](../../artifacts/audit/benchmarks/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.EnumerableCollectionMappingBenchmarks-report-github.md), [FlatMappingBenchmarks](../../artifacts/audit/benchmarks/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.FlatMappingBenchmarks-report-github.md), [NestedCollectionMappingBenchmarks](../../artifacts/audit/benchmarks/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.NestedCollectionMappingBenchmarks-report-github.md), [SetCollectionMappingBenchmarks](../../artifacts/audit/benchmarks/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.SetCollectionMappingBenchmarks-report-github.md).
- Full runner logs: [Validation](../../artifacts/audit/benchmarks-validation-full.log), [Mapping](../../artifacts/audit/benchmarks-mapping-full.log).
