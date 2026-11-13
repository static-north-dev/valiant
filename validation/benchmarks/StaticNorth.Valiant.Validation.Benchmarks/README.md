# Validation benchmarks

These benchmarks measure Valiant's fluent validation against Validly. Both implementations validate five independent properties with one
equivalent rule per property.

- `ValidationBenchmarks` measures full diagnostic result creation.
- `ValidationFailurePatternBenchmarks` alternates one/all failures or cycles
  through all 32 combinations, measuring result lookup after every rule executes.
- `ValidationStatusBenchmarks` measures the boolean API, which runs the same rules and diagnostics and returns validity directly.
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
are excluded. Full validation includes result creation or reuse, and Validly
result disposal is included because its result is pool-backed. The simple fluent workload uses generated checks and the bounded result cache in Auto and Fast.

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

## Runtime Async on .NET 11

```bash
dotnet run --project validation/benchmarks/StaticNorth.Valiant.Validation.Benchmarks --configuration Release -- --filter '*RuntimeAsyncBenchmarks*' --artifacts artifacts/runtime-async-benchmarks
```

BenchmarkDotNet compiles two jobs with Runtime Async off and on. Both use
the same fluent validators and successful `SatisfiesAsync` rule. The
nested case calls the single validator through `UseValidator`. The rule either
returns a completed `ValueTask` or suspends on a reusable completion source
that the benchmark completes after the validator yields. This models one
controlled suspension without I/O, thread-pool scheduling, or allocating a
completion source per call. Setup checks the expected compiler lowering and
completion behavior for both validators. The report includes operations per
second and managed bytes per operation.

Run the endpoint benchmark to measure the same validators through the Minimal API filter on Kestrel.

```bash
dotnet run --project validation/benchmarks/StaticNorth.Valiant.Validation.Benchmarks --configuration Release -- --filter '*RuntimeAsyncEndpointBenchmarks*' --artifacts artifacts/runtime-async-endpoint-benchmarks
```

The endpoint benchmark sends sequential HTTP requests over loopback from a
reused client. It binds a route parameter and resolves a scoped model and
validators through dependency injection. The suspended rule queues completion
to the thread pool after registering its continuation. Setup checks that valid
requests succeed and failed validation produces HTTP 400. Throughput is
reported in requests per second and allocations include both the client and
server in the benchmark process. The compiler switch applies to the generated
validator methods in the benchmark project. Both jobs use the same Valiant
endpoint filter and ASP.NET Core binaries.

### Concurrent Kestrel traffic

Build the benchmark twice, then run the load client from the classic build.

```bash
project=validation/benchmarks/StaticNorth.Valiant.Validation.Benchmarks
output=artifacts/runtime-async-kestrel-load
dotnet build "$project" -c Release -p:ValiantBenchmarkRuntimeAsync=false -o "$output/off"
dotnet build "$project" -c Release -p:ValiantBenchmarkRuntimeAsync=true -o "$output/on"
dotnet "$output/off/StaticNorth.Valiant.Validation.Benchmarks.dll" --runtime-async-kestrel-load \
  "$output/on/StaticNorth.Valiant.Validation.Benchmarks.dll" > "$output/trials.csv"
```

The client issues loopback HTTP/1.1 requests on 8 or 32 concurrent workers.
Each worker sends its next request as soon as the previous request finishes.
For each validator and rule completion behavior, the harness runs four paired
trials in fresh server processes and alternates which build runs first. Each
trial warms up with 80,000 requests, then measures 160,000 successful requests.

The CSV records total completed requests per second, nearest-rank p95 of
individual client-observed request times in microseconds, and managed bytes
allocated in the server process per request. The same client build drives
both servers, so its allocations are excluded. The client and server share
one machine. The scenario uses a small successful response and a scoped model
from DI, with thread-pool completion to simulate suspension.

BenchmarkDotNet reports are generated local artifacts, not a committed source
of truth. Their results depend on the workload, machine, runtime, and pinned
control version; retain or publish a snapshot only while those conditions are
documented and current.

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
bytes must decrease. For the boolean API, Valiant and Validly must return the same status before measurement.
