# Valiant performance, correctness, security, usability, and IDE audit

Date: 2026-09-26. Configuration means the **Settings** module in this repository.

## Verified result

| Module | Debug tests | Release tests |
| --- | ---: | ---: |
| Validation | 185 passed | 185 passed |
| Mapping | 89 passed | 89 passed |
| Settings | 40 passed | 40 passed |
| Endpoints | 208 passed | 208 passed |
| **Total** | **522 passed** | **522 passed** |

There are 29 new test cases over the initial 493-test baseline. No tests were skipped. Solution builds and the final endpoint rebuilds succeeded in both configurations with zero warnings and errors. All ten NuGet packages and their symbol packages passed structural verification, and all eight leaf package consumers passed. The NuGet advisory audit covered all 29 solution C# projects, including transitive dependencies, and reported no known vulnerable packages or audit errors.

Existing SDK, dependency-version, and lock-file edits were preserved. Verification used SDK **10.0.400**, Roslyn **5.9.0**, and .NET **10.0.11** on macOS ARM64.

## Defects found and fixed

- **Mapping and Settings diagnostic work repeated after unrelated edits.** Their value comparers were applied only to emission branches. Comparers now precede the diagnostic branches too; Settings equality includes diagnostic identity and location so errors remain current.
- **Endpoint candidates lacked value equality.** Candidates now compare registrations, query bindings, and diagnostic values, allowing the driver to cache downstream work.
- **Endpoint generation crashed across partial-class files.** A marker in one file and an inline query handler in another used the wrong semantic model. Each `Map` declaration now uses its own tree's semantic model. The added regression reproduced the crash before the fix.
- **Endpoint analysis bound calls without usable handler parameters.** A syntax precheck now avoids overload resolution when no supported inline handler has parameters. Existing query-binding behavior remains covered by the full endpoint suite.
- **Validation DI output depended on syntax-tree order.** Removing and restoring a file changed registration text. Generated validator and property-validator registrations are now ordered by fully qualified validator type. Consumers enumerating those generated registrations now receive that deterministic order.
- **Settings schemas omitted inherited properties.** Public getters on base types are included, derived declarations take precedence, private getters are excluded, and secret markers survive overrides.
- **Settings document callers could reveal secrets without permission.** Both the reveal request and permission flag are now required. The HTTP endpoint already checked its permission flag; the defect affected direct factory callers.
- **Settings errors and unmodeled runtime objects could expose sensitive data.** Exception messages are no longer returned, masked getters are not invoked, and recursive or opaque objects cannot bypass schema-based projection. Unsupported opaque values become `null`; object branches beyond available schema metadata become empty objects. Schema responses use `Cache-Control: no-store`.
- **Package smoke projects pinned an incompatible compiler.** Their separate Roslyn 5.6 override produced CS9057 against the updated 5.9 generators and SDK analyzers. They now use the exact compiler supplied by `global.json`.

## Generator discovery, change detection, and memory checks

The shared `GeneratorIdeTestHarness` is linked into each module's existing test project, so these checks run in normal CI. It preserves unchanged syntax-tree instances and reuses the same generator driver, matching document-edit behavior more closely than rebuilding the entire compilation from text.

For each module it tests 1, 32, and 128 marked declarations alongside 512 unrelated files:

- An unchanged compilation, ten successive unrelated edits, and trailing trivia in a marked file keep **every registered output callback Cached**. Generated source and diagnostics stay identical. Across the four scale matrices, this covers 120 successive unrelated-edit runs.
- Editing a dependency in another file invalidates exactly one item model and changes output. Incremental source and diagnostics are compared against a fresh driver.
- Invalid declarations, diagnostic line moves, file moves, removal, restoration, and undo match fresh generation. Valid states also compile successfully.
- A canceled driver invocation throws cancellation and leaves the prior immutable driver usable.
- Tracked models and registered output inputs are traversed recursively. They must not retain compilations, semantic models, symbols, syntax nodes, syntax trees, or Roslyn `Location` objects.
- A separate endpoint case checks partial declarations with the marker and `Map` method in different files.

Discovery remains attribute-scoped. Endpoint query-binding opt-in detection retains its narrow syntax predicate; this audit does not replace it with compilation-wide discovery. These choices follow [Roslyn's incremental generator guidance](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md): value equality stops downstream work, but compilation changes can still require semantic transforms to rerun.

## Measured IDE workload

Release runs were executed sequentially, with one test at a time. Each edit column is the median of ten driver runs. Parsing the edited text, building metadata references, compilation validation, and test assertions are outside the measured interval. Roslyn step tracking is enabled. The desktop and its other applications were not isolated.

| Generator | 1 declaration | 32 declarations | 128 declarations | Temporary allocation/edit at 128 | No-op at 128 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Validation | 5.13 ms | 17.61 ms | 42.97 ms | 5.36 MiB | 3.90 ms |
| Mapping | 4.61 ms | 16.45 ms | 45.19 ms | 4.77 MiB | 3.06 ms |
| Settings | 5.34 ms | 12.52 ms | 30.08 ms | 2.92 MiB | 2.51 ms |
| Endpoints, final run | 8.58 ms | 26.57 ms | 95.28 ms | 3.87 MiB | 12.67 ms |

Endpoint allocation fell from 6.65 MiB to 3.87 MiB per unrelated edit at 128 declarations, approximately **42%**. Its measured median time was 90.68 ms before the syntax precheck, 59.35 ms in the first run after it, and 95.28 ms in the final run. This variability prevents a reliable wall-clock speedup claim. The final endpoint run's slowest edit was 149.06 ms.

Allocations are measured using `GC.GetAllocatedBytesForCurrentThread`; they are temporary driver allocations, **not retained IDE heap or process RSS**. Cached emission does not mean zero semantic work or zero allocation. These results do not establish a universal “no IDE slowdown” guarantee, and endpoint-heavy solutions remain the priority for actual IDE-host profiling. CI gates caching and correctness, not noisy elapsed-time thresholds.

## Runtime performance samples

| Module / operation | Observed time | Managed allocation |
| --- | ---: | ---: |
| Validation generated `IsValid`, three existing scenarios | 2.23–3.08 ns/op | 0 B/op reported |
| Mapping generated flat mapping | 10.18 ns/op | 96 B/op |
| Settings masked one-property runtime document | 1.70 µs/op | 1,120 B/op |
| Endpoints mixed scalar/array query parsing | 4.66 µs/op | 3,784 B/op |

Validation and Mapping use their existing BenchmarkDotNet benchmarks with the in-process ShortRun toolchain, three warmups and three measured iterations. Confidence intervals are wide; these are representative samples, not precise latency commitments. Settings and Endpoints use 100 warmup calls followed by 10,000 measured calls. Those timings likewise are observational, with no hardware-dependent pass/fail threshold. BenchmarkDotNet's installed-SDK inventory displays an older side-by-side SDK; the invoked build SDK was explicitly verified as 10.0.400 and the in-process host reports .NET 10.0.11.

## Security and usability coverage

The full suites cover validation rejection before handlers, severity and mode parity, mapping null and collection behavior, invalid authoring diagnostics, endpoint binding errors and conservative parsing defaults, settings authorization, and explicit secret reveal. Added adversarial query cases cover scalar overflow, malformed encoded values, and a sparse array index of `Int32.MaxValue` without allocating an array of that length. Added Settings checks cover denied direct reveals, exception-message redaction, masked getters, recursive and opaque values, inherited secrets, and non-cacheable responses.

The actual Settings sample was exercised in the in-app browser: loading, section search, selection, property filtering, JSON view, no-match state, and clearing search worked. The narrow 536-pixel layout was visually inspected; no browser warnings or errors were recorded. Other modules' authoring and integration usability is covered by compiled samples and HTTP integration tests. The temporary server and browser tab were closed.

This is a code and regression audit with representative measurements, not an exhaustive penetration test or accessibility certification. Windows/Linux, long IDE sessions, actual Rider/Visual Studio CPU and retained-memory profiles, and Native AOT publication were not exercised. Current generator assemblies require a compatible Roslyn 5.9 host; the `netstandard2.0` assembly target does not make them loadable in older compiler hosts.

## Reproduction and evidence

Run with the SDK pinned by `global.json`:

```sh
dotnet build StaticNorth.Valiant.sln -c Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet test --solution StaticNorth.Valiant.sln -c Release --no-build --maximum-parallel-tests 4 --report-trx
dotnet build StaticNorth.Valiant.sln -c Debug --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet test --solution StaticNorth.Valiant.sln -c Debug --no-build --maximum-parallel-tests 4 --report-trx
dotnet msbuild StaticNorth.Valiant.Pack.proj -t:VerifyPackageConsumers -m:1 -p:Configuration=Release -p:NoBuild=true
```

For each module, run its test project sequentially with:

```sh
dotnet run --project <module-test-project> -c Release --no-build -- --treenode-filter '/*/*/GeneratorIdeTests/*' --maximum-parallel-tests 1 --report-trx
```

Local evidence is retained under `artifacts/audit/` (ignored build output):

- `tests-release.log`, `tests-debug.log`, and per-module TRX/HTML files in `release/` and `debug/`.
- `generator-measurements.csv`, `ide-serial/`, and `ide-endpoints-final/`.
- `benchmarks/`, `benchmark-validation.log`, `benchmark-mapping.log`, and `runtime/`.
- `nuget-audit.json` and `package-consumers.log`.
- `before/`, `security-before/`, `security-object-before/`, and `partial-before/` retain failing-regression evidence. The initial `before/` run also exposed a test-harness issue with default immutable arrays, which was corrected before final verification.

Sandbox restrictions intermittently prevented compiler/test named pipes; final tests and package checks ran with the required local IPC permissions. An explicit initial build resolved a zero-test discovery run. These environment failures were not counted as successful test runs.
