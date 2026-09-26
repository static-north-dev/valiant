# Validation allocation optimization

The benchmark validator now matches Validly's zero managed allocation after warmup for both one failure and all five failures. Generated validators reuse immutable failure content for small, fixed rule sets while rerunning validation checks on every call. Callers can retain results indefinitely without disposal.

## Measured results

| Scenario | Previous Valiant mean | Updated Valiant mean | Validly mean | Previous Valiant allocation | Updated Valiant allocation | Validly allocation |
|---|---:|---:|---:|---:|---:|---:|
| Valid | 3.753 ns | 2.530 ns | 56.698 ns | 0 B | 0 B | 0 B |
| OneFailure | 20.248 ns | 3.457 ns | 58.104 ns | 96 B | 0 B | 0 B |
| AllFailures | 47.176 ns | 4.927 ns | 69.369 ns | 256 B | 0 B | 0 B |

The previous values are from the [owned-result optimization](2026-09-26-validation-failure-optimization.md). The updated values and Validly control were measured together with BenchmarkDotNet 0.15.8, SDK 10.0.400, .NET 10.0.11, macOS 27.0, and Arm64 RyuJIT. The Release in-process runner used five warmups and ten measured iterations; BenchmarkDotNet applied its normal outlier removal. All ten selected cases completed successfully. These are local microbenchmarks, not a guarantee for other workloads or runtimes.

Two additional workloads exercise changing inputs instead of repeating one failure pattern:

| Workload | Valiant mean | Validly mean | Valiant allocation | Validly allocation |
|---|---:|---:|---:|---:|
| Alternate one failure and all five failures | 3.914 ns | 66.352 ns | 0 B | 0 B |
| Cycle through all 32 combinations, including valid input | 4.592 ns | 69.897 ns | 0 B | 0 B |

Setup verifies equivalent validity and failed properties before measurement. Both implementations execute their five checks during every timed operation. Validly's result disposal remains included in timing. Validator/input construction and first-use result allocations are excluded; both implementations reuse storage during measurement.

## Scope and memory tradeoff

Reuse applies to generated synchronous validators with a flat list of one to six checks, fixed property paths, default codes/messages/severities, and messages that do not require runtime formatting. Their synchronous rules also benefit through `ValidateAsync`, which delegates to the same generated method. Predicates, conditions, property getters, and rule arguments still execute normally. Only the resulting immutable diagnostic content is reused; model instances and validation decisions are never cached.

Custom code, message, or severity expressions, culture-dependent formatting, composition/control-flow nodes, asynchronous checks, and validators above six checks retain the owned accumulator path. Direct `ValidationResult` construction also retains its previous allocation. This change does not promise zero allocation for those paths.

Each eligible validator type retains one fixed descriptor snapshot and a result table. Results are created lazily for observed nonempty failure combinations; a six-check validator has at most 63 such results, and the benchmark's five-check validator has at most 31. Thread-safe publication uses a volatile read and compare/exchange. Concurrent first encounters may construct temporary duplicate results, but all callers receive the same published immutable result for a combination.

First encounters allocate. Cold-allocation regression budgets cover cache construction and materialization of every combination: at most 6,000 B for five checks and 13,000 B for six checks, excluding the caller-supplied descriptor array and strings. These are single-thread managed allocation bounds, not process RSS or IDE memory measurements. Most validators see only a subset of combinations and create fewer results.

Generator discovery, incremental analysis, and fingerprints are unchanged. Eligibility is decided from the existing per-validator value model during emission, with literal inspection bounded to six checks. The change adds no compilation-wide scans or retained Roslyn state.

## Verification

- Debug and Release builds of `StaticNorth.Valiant.Release.slnf` passed with zero warnings and errors, including both runtime target frameworks.
- The complete suite passed **554/554 in each configuration**: Validation 217, Mapping 89, Settings 40, and Endpoints 208.
- New regression coverage verifies zero steady-state allocation, all failure combinations, complete ordered fast/runtime failure parity, changing model values, getter execution, sync/async result reuse, fail-fast behavior, dynamic-message reevaluation, descriptor snapshots, severity partitions, concurrent publication, and bounded cache size/cold allocation.
- All four modules' IDE incrementality, change detection, cancellation, fresh-generation comparison, and value-only output checks remain green.
- All eight NuGet package consumers passed with the updated generator and runtime. `git diff --check` passed.

## Evidence

- [Full-result BenchmarkDotNet summary](../../artifacts/audit/benchmarks/validation-failure-cache/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationBenchmarks-report-github.md), [CSV](../../artifacts/audit/benchmarks/validation-failure-cache/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationBenchmarks-report.csv), and [HTML](../../artifacts/audit/benchmarks/validation-failure-cache/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationBenchmarks-report.html).
- [Changing-input summary](../../artifacts/audit/benchmarks/validation-failure-cache/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationFailurePatternBenchmarks-report-github.md), [CSV](../../artifacts/audit/benchmarks/validation-failure-cache/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationFailurePatternBenchmarks-report.csv), and [HTML](../../artifacts/audit/benchmarks/validation-failure-cache/results/StaticNorth.Valiant.Validation.Benchmarks.ValidationFailurePatternBenchmarks-report.html).
- [Benchmark runner log](../../artifacts/audit/benchmark-validation-failure-cache.log).
- [Release validation test report](../../artifacts/audit/failure-cache-tests-release/StaticNorth.Valiant.Validation.Tests-macos-net10.0-report.html) and [Debug validation test report](../../artifacts/audit/failure-cache-tests-debug/StaticNorth.Valiant.Validation.Tests-macos-net10.0-report.html).
- [Package consumer log](../../artifacts/audit/failure-cache-package-consumers.log).
