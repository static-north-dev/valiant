---
title: Samples
description: Run the core validation and ASP.NET Core integration samples.
---

# Validation samples

Validation has separate console and ASP.NET Core sample projects. Each runnable
choice owns its description on its `Sample` or `Startup` class; `Program.cs`
only selects which one to execute.

## Core validation

Run the console project:

```bash
dotnet run --project validation/samples/StaticNorth.Valiant.Validation.Samples
```

Select one delegate in
[`Program.cs`](https://github.com/static-north-dev/valiant/blob/main/validation/samples/StaticNorth.Valiant.Validation.Samples/Program.cs):

| Delegate | Demonstrates | Source |
| --- | --- | --- |
| `Basic.Sample.RunAsync` | A minimal fluent validator, generated registration, validation, and failure output | [`Basic`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.Samples/Basic) |
| `AttributeValidation.Sample.RunAsync` | Validation attributes on the model with the generated validator contract | [`AttributeValidation`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.Samples/AttributeValidation) |
| `DependencyInjection.Sample.RunAsync` | Generated registrations, configurable lifetime and defaults, concrete non-default validators, property validators, and metadata providers | [`DependencyInjection`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.Samples/DependencyInjection) |
| `SyncAndAsyncValidation.Sample.RunAsync` | Sync and async APIs, boolean helpers, and the guard against synchronously invoking an asynchronous validator | [`SyncAndAsyncValidation`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.Samples/SyncAndAsyncValidation) |
| `Severity.Sample.RunAsync` | Error, warning, and information severities, including valid warning-only and information-only results | [`Severity`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.Samples/Severity) |
| `CompositionAndPaths.Sample.RunAsync` | Whole-model composition, reusable property validators, child contexts, collection items, and nested/indexed paths | [`CompositionAndPaths`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.Samples/CompositionAndPaths) |
| `FailureHandling.Sample.RunAsync` | Per-property fail-fast behavior, result helpers, and `ThrowIfFailed()` | [`FailureHandling`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.Samples/FailureHandling) |
| `ControlFlowAndCollections.Sample.RunAsync` | Collection and dictionary rules, conditions, switches, indexed validation, and native loops | [`ControlFlowAndCollections`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.Samples/ControlFlowAndCollections) |

The
[`Catalog`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.Samples/Catalog)
folder is shared support code for the advanced scenarios, not an additional
selectable sample.

## ASP.NET Core validation

Run the web project:

```bash
dotnet run --project validation/samples/StaticNorth.Valiant.Validation.AspNetCore.Samples
```

Select one startup in
[`Program.cs`](https://github.com/static-north-dev/valiant/blob/main/validation/samples/StaticNorth.Valiant.Validation.AspNetCore.Samples/Program.cs):

| Startup | Demonstrates | Source |
| --- | --- | --- |
| `MinimalApi.Startup` | Typed and all-parameter Minimal API filters that reject invalid requests before the handler runs | [`MinimalApi`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.AspNetCore.Samples/MinimalApi) |
| `Mvc.Startup` | Automatic MVC validation without a validator dependency or manual validation branch in the controller | [`Mvc`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.AspNetCore.Samples/Mvc) |
| `ProblemDetails.Startup` | Manual validation converted to HTTP and MVC validation problem-details responses | [`ProblemDetails`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.AspNetCore.Samples/ProblemDetails) |
| `OpenApi.Startup` | Generated Valiant validation metadata projected into the application's OpenAPI document | [`OpenApi`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.AspNetCore.Samples/OpenApi) |

The
[`Orders`](https://github.com/static-north-dev/valiant/tree/main/validation/samples/StaticNorth.Valiant.Validation.AspNetCore.Samples/Orders)
folder contains the shared request models and validator; it is not a startup
choice.

The web scenarios are also exercised through `WebApplicationFactory`, so the
sampled registration and HTTP behavior stay covered by the validation test
project. See [ASP.NET Core and OpenAPI](./guides/aspnetcore-openapi.md) for
consumer setup.
