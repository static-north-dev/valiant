# Valiant full benchmark report

Complete current rerun of every benchmark project in the repository. This follows the Validation immutable-result reuse change.

Run on 2026-09-26 with BenchmarkDotNet 0.15.8, .NET SDK 10.0.400, .NET runtime 10.0.11, macOS 27.0, Arm64 RyuJIT. Both suites used the in-process toolchain with five warmups and ten measured iterations per case. Every discovered benchmark case completed successfully.

| Module | Benchmark classes | Cases | Status |
|---|---:|---:|---|
| Validation | 5 | 21 | Complete |
| Mapping | 5 | 26 | Complete |
| Settings (Configuration) | 0 | 0 | No benchmark project exists |
| Endpoints | 0 | 0 | No benchmark project exists |

The three Settings and Endpoints rows mean those modules have no BenchmarkDotNet projects to run; they are not failed cases. This report includes every discovered Validation and Mapping case, including result access/construction, status-only validation, and the 32-pattern failure workload.

The Validation full-result workload measured one failure at 3.457 ns and all five failures at 4.927 ns, both at 0 B after warmup. Its 32-combination workload measured 4.592 ns and 0 B. Dynamic diagnostics and cold cache construction are outside these warm-result cases; the [allocation report](2026-09-26-validation-allocation-optimization.md) documents eligibility and cold-allocation bounds.

Valiant was faster than Mapperly in all 13 paired Mapping mean comparisons. Managed allocations were equal or lower across the paired cases. Mapping allocates target objects and collection storage as part of the measured operation.

## Validation

### ValidationBenchmarks

[BenchmarkDotNet summary](../../artifacts/audit/benchmarks/all-final/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationBenchmarks-report-github.md) · [CSV](../../artifacts/audit/benchmarks/all-final/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationBenchmarks-report.csv)

| Benchmark | Parameters | Mean | 99.9% CI half width | Allocated / operation |
|---|---|---:|---:|---:|
| ValiantFast | Scenario=Valid | 2.530 ns | 0.0237 ns | 0 B |
| Validly | Scenario=Valid | 56.698 ns | 0.5762 ns | 0 B |
| ValiantFast | Scenario=OneFailure | 3.457 ns | 0.0305 ns | 0 B |
| Validly | Scenario=OneFailure | 58.104 ns | 0.5076 ns | 0 B |
| ValiantFast | Scenario=AllFailures | 4.927 ns | 0.0238 ns | 0 B |
| Validly | Scenario=AllFailures | 69.369 ns | 0.2437 ns | 0 B |

### ValidationFailurePatternBenchmarks

[BenchmarkDotNet summary](../../artifacts/audit/benchmarks/all-final/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationFailurePatternBenchmarks-report-github.md) · [CSV](../../artifacts/audit/benchmarks/all-final/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationFailurePatternBenchmarks-report.csv)

| Benchmark | Parameters | Mean | Error | Allocated / operation |
|---|---|---:|---:|---:|
| Validly | PatternCount=2 | 66.352 ns | 0.4923 ns | 0 B |
| ValiantFast | PatternCount=2 | 3.914 ns | 0.0348 ns | 0 B |
| Validly | PatternCount=32 | 69.897 ns | 0.4933 ns | 0 B |
| ValiantFast | PatternCount=32 | 4.592 ns | 0.0362 ns | 0 B |

### ValidationResultAccessBenchmarks

[BenchmarkDotNet summary](../../artifacts/audit/benchmarks/all-final/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationResultAccessBenchmarks-report-github.md) · [CSV](../../artifacts/audit/benchmarks/all-final/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationResultAccessBenchmarks-report.csv)

| Benchmark | Parameters | Mean | Error | Allocated / operation |
|---|---|---:|---:|---:|
| EnumeratePartitions | — | 8.244 ns | 0.0409 ns | 0 B |
| IndexedPartitionAccess | — | 10.391 ns | 0.0806 ns | 0 B |

### ValidationResultConstructionBenchmarks

[BenchmarkDotNet summary](../../artifacts/audit/benchmarks/all-final/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationResultConstructionBenchmarks-report-github.md) · [CSV](../../artifacts/audit/benchmarks/all-final/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationResultConstructionBenchmarks-report.csv)

| Benchmark | Parameters | Mean | Error | Allocated / operation |
|---|---|---:|---:|---:|
| Construct | Scenario=OneError | 14.24 ns | 0.086 ns | 96 B |
| Construct | Scenario=FiveErrors | 48.64 ns | 0.761 ns | 256 B |
| Construct | Scenario=MixedSeverities | 80.81 ns | 0.422 ns | 424 B |

### ValidationStatusBenchmarks

[BenchmarkDotNet summary](../../artifacts/audit/benchmarks/all-final/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationStatusBenchmarks-report-github.md) · [CSV](../../artifacts/audit/benchmarks/all-final/validation/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationStatusBenchmarks-report.csv)

| Benchmark | Parameters | Mean | Error | Allocated / operation |
|---|---|---:|---:|---:|
| ValiantGeneratedStatus | Scenario=Valid | 2.260 ns | 0.0598 ns | 0 B |
| ValidlyStatus | Scenario=Valid | 57.495 ns | 0.3392 ns | 0 B |
| ValiantGeneratedStatus | Scenario=OneFailure | 2.013 ns | 0.0253 ns | 0 B |
| ValidlyStatus | Scenario=OneFailure | 58.046 ns | 0.4133 ns | 0 B |
| ValiantGeneratedStatus | Scenario=AllFailures | 9.773 ns | 0.2273 ns | 0 B |
| ValidlyStatus | Scenario=AllFailures | 70.176 ns | 0.7149 ns | 0 B |

## Mapping

### ArrayCollectionMappingBenchmarks

[BenchmarkDotNet summary](../../artifacts/audit/benchmarks/all-final/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.ArrayCollectionMappingBenchmarks-report-github.md) · [CSV](../../artifacts/audit/benchmarks/all-final/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.ArrayCollectionMappingBenchmarks-report.csv)

| Benchmark | Parameters | Mean | Error | Allocated / operation |
|---|---|---:|---:|---:|
| ValiantArray | CollectionSize=0 | 7.670 ns | 0.0899 ns | 48 B |
| MapperlyArray | CollectionSize=0 | 8.138 ns | 0.0611 ns | 48 B |
| ValiantArray | CollectionSize=10 | 79.904 ns | 0.3421 ns | 608 B |
| MapperlyArray | CollectionSize=10 | 83.991 ns | 2.9904 ns | 608 B |
| ValiantArray | CollectionSize=100 | 696.427 ns | 7.3001 ns | 5648 B |
| MapperlyArray | CollectionSize=100 | 722.972 ns | 5.5890 ns | 5648 B |

### EnumerableCollectionMappingBenchmarks

[BenchmarkDotNet summary](../../artifacts/audit/benchmarks/all-final/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.EnumerableCollectionMappingBenchmarks-report-github.md) · [CSV](../../artifacts/audit/benchmarks/all-final/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.EnumerableCollectionMappingBenchmarks-report.csv)

| Benchmark | Parameters | Mean | Error | Allocated / operation |
|---|---|---:|---:|---:|
| ValiantEnumerable | CollectionSize=0 | 8.340 ns | 0.0968 ns | 48 B |
| MapperlyEnumerable | CollectionSize=0 | 29.319 ns | 0.2899 ns | 88 B |
| ValiantEnumerable | CollectionSize=10 | 81.073 ns | 0.7222 ns | 608 B |
| MapperlyEnumerable | CollectionSize=10 | 109.350 ns | 0.8931 ns | 720 B |
| ValiantEnumerable | CollectionSize=100 | 701.023 ns | 5.7194 ns | 5648 B |
| MapperlyEnumerable | CollectionSize=100 | 803.890 ns | 5.6612 ns | 5760 B |

### FlatMappingBenchmarks

[BenchmarkDotNet summary](../../artifacts/audit/benchmarks/all-final/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.FlatMappingBenchmarks-report-github.md) · [CSV](../../artifacts/audit/benchmarks/all-final/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.FlatMappingBenchmarks-report.csv)

| Benchmark | Parameters | Mean | Error | Allocated / operation |
|---|---|---:|---:|---:|
| ValiantFast | — | 7.763 ns | 0.0634 ns | 96 B |
| Mapperly | — | 7.975 ns | 0.1628 ns | 96 B |

### NestedCollectionMappingBenchmarks

[BenchmarkDotNet summary](../../artifacts/audit/benchmarks/all-final/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.NestedCollectionMappingBenchmarks-report-github.md) · [CSV](../../artifacts/audit/benchmarks/all-final/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.NestedCollectionMappingBenchmarks-report.csv)

| Benchmark | Parameters | Mean | Error | Allocated / operation |
|---|---|---:|---:|---:|
| ValiantFast | CollectionSize=0 | 27.37 ns | 0.336 ns | 216 B |
| Mapperly | CollectionSize=0 | 29.14 ns | 0.324 ns | 216 B |
| ValiantFast | CollectionSize=10 | 101.18 ns | 0.984 ns | 800 B |
| Mapperly | CollectionSize=10 | 174.65 ns | 1.640 ns | 840 B |
| ValiantFast | CollectionSize=100 | 717.53 ns | 3.556 ns | 5840 B |
| Mapperly | CollectionSize=100 | 1,320.16 ns | 14.777 ns | 5880 B |

### SetCollectionMappingBenchmarks

[BenchmarkDotNet summary](../../artifacts/audit/benchmarks/all-final/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.SetCollectionMappingBenchmarks-report-github.md) · [CSV](../../artifacts/audit/benchmarks/all-final/mapping/results/StaticNorth.Valiant.Mapping.Benchmarks.SetCollectionMappingBenchmarks-report.csv)

| Benchmark | Parameters | Mean | Error | Allocated / operation |
|---|---|---:|---:|---:|
| ValiantSet | CollectionSize=0 | 15.75 ns | 0.173 ns | 152 B |
| MapperlySet | CollectionSize=0 | 52.41 ns | 0.431 ns | 272 B |
| ValiantSet | CollectionSize=10 | 306.79 ns | 4.043 ns | 904 B |
| MapperlySet | CollectionSize=10 | 558.72 ns | 5.100 ns | 1488 B |
| ValiantSet | CollectionSize=100 | 2,879.30 ns | 38.403 ns | 7144 B |
| MapperlySet | CollectionSize=100 | 4,657.91 ns | 62.161 ns | 12424 B |

## Run artifacts

- [Validation runner log](../../artifacts/audit/benchmark-all-final-validation.log).
- [Mapping runner log](../../artifacts/audit/benchmark-all-final-mapping.log).
- [Previous full benchmark baseline](2026-09-26-full-benchmarks.md).
- [Validation allocation optimization details](2026-09-26-validation-allocation-optimization.md).

BenchmarkDotNet reports managed allocations per invocation. An em dash in an artifact summary denotes no managed allocation detected. Results are local microbenchmarks: in-process measurements reduce launcher and process variation, but results still depend on the machine, runtime, workload, and JIT state.
