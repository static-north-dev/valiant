---
name: valiant-validation
description: Author and integrate StaticNorth.Valiant validators in .NET, including fluent or attribute rules, async validation, composition, and generated registration.
---

# Valiant Validation

Determine the application's `StaticNorth.Valiant.*` package major before choosing examples. Use the matching version from [the documentation index](https://valiant.staticnorth.dev/agents/documents.json); the default is the latest published major, while `current` is development documentation.

Read the matching quick start and authoring guide before editing. The latest published versions are [Quick start](https://valiant.staticnorth.dev/validation/quick-start) and [Authoring rules](https://valiant.staticnorth.dev/validation/guides/authoring-rules).

- Install `StaticNorth.Valiant.Validation`, or the specific integration package when the application uses ASP.NET Core, OpenAPI, Mediator, or MediatR. Integration packages carry the required parent runtime and analyzer assets.
- Mark a `partial` validator with `[ValiantValidator]` and derive from `ValidatorDefinition<TModel>`.
- Choose one authoring model: omit `Configure` for model validation attributes, or override the instance `Configure(ValidationContext<TModel>)` for fluent rules. Do not combine these models in one validator.
- Register generated services with `AddValiantValidators()` from `StaticNorth.Valiant.Validation.DependencyInjection`. Preserve explicit discovery and generated registration; do not introduce assembly scanning.
- Use `ValidateAsync` whenever a rule or composed validator can run asynchronously. Preserve cancellation and ordered failure severity when adding rules.
- Reuse model validators through `ValidationContext<T>.UseValidator`. Property-value reuse requires an `IPropertyValidator<TProperty>`; it is a separate contract.
- For Native AOT Minimal APIs, prefer the normal generated invocation bridges supplied by `[ValiantValidator]` and `AddValiantValidators()`. The typed endpoint overload is the explicit manual AOT path.

When a generated implementation and debuggable runtime implementation both exist, verify the new behavior independently in each mode. Include the property path, code, message, and severity when comparing failures. Consult the application's existing tests and build commands rather than assuming a particular test runner.
