# Validation failure-path optimization

This report records the initial owned-result optimization. The later [bounded immutable-result reuse report](2026-09-26-validation-allocation-optimization.md) contains the current timings and zero-allocation measurements for eligible generated validators.

The generated full-result validation path now takes less time than Validly in both the one-failure and all-failures benchmark cases, with substantially less allocation than Valiant's original implementation. Results remain owned and immutable; callers do not need to dispose them.

## Measured results

| Scenario | Original Valiant mean | Updated Valiant mean | Validly mean in updated run | Original Valiant allocation | Updated Valiant allocation | Validly allocation |
|---|---:|---:|---:|---:|---:|---:|
| Valid | 3.756 ns | 3.753 ns | 57.634 ns | 0 B | 0 B | 0 B |
| OneFailure | 45.220 ns | 20.248 ns | 58.373 ns | 216 B | 96 B | 0 B |
| AllFailures | 110.567 ns | 47.176 ns | 70.179 ns | 344 B | 256 B | 0 B |

Compared with the original Valiant run, one failure takes approximately 55% less time and allocates 56% less memory. All five failures take approximately 57% less time and allocate 26% less memory. Valid inputs retain the original speed and zero-allocation behavior.

The updated measurements use BenchmarkDotNet 0.15.8, .NET SDK 10.0.400, .NET runtime 10.0.11, Arm64 RyuJIT, and macOS 27.0. The Release benchmark was built from the updated source before measurement. The in-process toolchain ran five warmups and ten measured iterations; the original run used three of each. The default out-of-process attempt stalled at package restore and was stopped. These are local microbenchmark measurements, not a guarantee for every runtime or workload. The same updated run includes Validly as the comparison baseline.

Both full-result benchmarks execute all five configured rules and assert the expected failure properties during setup. Validly returns pooled results through `Dispose`, so its zero allocation reflects reuse in steady-state measurements. Valiant's remaining 96 B and 256 B pay for independently retained immutable results; this change does not introduce pooling or cache validation outcomes.

The affected result-construction and partition-access benchmark classes were also rerun with the same five warmups and ten measurements:

| Benchmark | Scenario | Mean | Managed allocation |
|---|---|---:|---:|
| Construct | OneError | 14.54 ns | 96 B |
| Construct | FiveErrors | 49.14 ns | 256 B |
| Construct | MixedSeverities | 84.34 ns | 424 B |
| EnumeratePartitions | Mixed severities | 8.483 ns | 0 B |
| IndexedPartitionAccess | Mixed severities | 9.444 ns | 0 B |

## Implementation

- Generated synchronous and asynchronous validators accumulate failures in a value-type local. The existing public `ValidationResultBuilder` class remains available and delegates to the same accumulation logic, preserving compatibility for earlier generated consumers.
- A single failure uses a compact immutable list instead of a separate one-element array and read-only wrapper. Multiple failures retain the existing owned-array and read-only partition behavior.
- The accumulator's append path and failure-value constructor allow inlining. Buffer growth stays off the normal append path, and error count is derived from total, warning, and info counts instead of incrementing a redundant counter for every error.
- `IsValid` checks the stored error state directly, avoiding collection-interface dispatch. Severity partitions and the serialized JSON object shape remain unchanged.

Generator discovery and analysis pipelines are unchanged. This modifies emission and runtime result construction; it adds no compilation-wide scans or retained Roslyn state.

## Verification

Debug and Release builds of the C# solution filter passed with zero warnings and errors. The complete solution test suite passed 530/530 in each configuration, including generated/runtime mode parity, async behavior, fail-fast handling, ordered severity partitions, retained-result immutability, generator incrementality/change detection, and new allocation and JSON regressions. All eight package consumer checks passed with the updated runtime and generator.

New allocation regression cases enforce zero bytes for empty accumulation, at most 96 bytes for one failure, and at most 256 bytes for five failures per result after warmup. They run in both Debug and Release.

## Evidence

- [Original full benchmark report](2026-09-26-full-benchmarks.md).
- [Updated BenchmarkDotNet summary](../../artifacts/audit/benchmarks/validation-failure-final/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationBenchmarks-report-github.md), [CSV](../../artifacts/audit/benchmarks/validation-failure-final/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationBenchmarks-report.csv), and [HTML](../../artifacts/audit/benchmarks/validation-failure-final/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationBenchmarks-report.html).
- [Updated benchmark runner log](../../artifacts/audit/benchmark-validation-failure-final-inprocess.log).
- Updated [result-construction summary](../../artifacts/audit/benchmarks/validation-results-final/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationResultConstructionBenchmarks-report-github.md), [partition-access summary](../../artifacts/audit/benchmarks/validation-results-final/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationResultAccessBenchmarks-report-github.md), and [runner log](../../artifacts/audit/benchmark-validation-results-final.log).
- [Release validation test report](../../artifacts/audit/failure-optimization-tests-release/StaticNorth.Valiant.Validation.Tests-macos-net10.0-report.html) and [Debug validation test report](../../artifacts/audit/failure-optimization-tests-debug/StaticNorth.Valiant.Validation.Tests-macos-net10.0-report.html).
- [Package consumer log](../../artifacts/audit/failure-optimization-package-consumers.log).
