# Validation samples

The validation samples use the same layout as the endpoint and settings samples:
each feature has one top-level folder, its description lives on the `Sample` or
`Startup` class, and `Program.cs` selects the example to run.

## Core samples

Choose one delegate near the top of
`StaticNorth.Valiant.Validation.Samples/Program.cs`, then run:

```bash
dotnet run --project validation/samples/StaticNorth.Valiant.Validation.Samples
```

Available samples:

- `Basic`: a minimal fluent validator and its failures.
- `AttributeValidation`: validation rules declared on the model.
- `DependencyInjection`: generated registrations, lifetimes, defaults, property validators, and metadata.
- `SyncAndAsyncValidation`: sync and async APIs plus the synchronous guard.
- `Severity`: errors, warnings, and informational failures.
- `CompositionAndPaths`: whole-model and property-validator reuse with nested/indexed paths.
- `FailureHandling`: per-chain fail-fast behavior and `ThrowIfFailed`.
- `ControlFlowAndCollections`: collection, dictionary, conditional, switch, indexed-item, and native-loop rules.

The `Catalog` folder contains the shared model, reusable validators, and test
data used by the advanced samples.

## ASP.NET Core samples

Choose one startup type near the top of
`StaticNorth.Valiant.Validation.AspNetCore.Samples/Program.cs`, then run:

```bash
dotnet run --project validation/samples/StaticNorth.Valiant.Validation.AspNetCore.Samples
```

Available samples:

- `MinimalApi`: typed and all-argument endpoint filters.
- `Mvc`: automatic MVC validation without manual controller validation.
- `ProblemDetails`: HTTP and MVC validation problem-details conversion.
- `OpenApi`: generated validation constraints in `/openapi/v1.json`.

The `Orders` folder contains the shared request model and validator. Each web
startup is also exercised through `WebApplicationFactory` in the validation
test project.
