# Valiant

Valiant is a family of source-generated .NET libraries. Declare validation rules, routes, mappings, or settings in C# and get generated implementations, registration, and build-time diagnostics.

| Module | What it does | Start here |
| --- | --- | --- |
| **Validation** | Turn fluent rules or model attributes into typed validators. Step through fluent rules in Debug and use generated checks in Release. | [Validate a request](https://valiant.staticnorth.dev/validation/quick-start) |
| **Endpoints** | Organize ASP.NET Core Minimal APIs into classes and groups with generated route registration. | [Map a route](https://valiant.staticnorth.dev/endpoints/quick-start) |
| **Mapping** | Map objects into records and other DTOs with generated assignments, conventions, and explicit rules. | [Map a DTO](https://valiant.staticnorth.dev/mapping/quick-start) |
| **Settings** | Generate schemas from typed configuration and opt into Options binding, validation, or an ASP.NET Core explorer. | [Bind settings](https://valiant.staticnorth.dev/settings/quick-start) |

Use the modules independently or [combine them in one Minimal API](https://valiant.staticnorth.dev/endpoints/guides/validated-api). The [package and compatibility guide](https://valiant.staticnorth.dev/validation/reference/packages) covers the build SDK, target frameworks, integration packages, and Native AOT paths.

[Documentation](https://valiant.staticnorth.dev/) · [NuGet packages](https://www.nuget.org/profiles/staticNorth) · [Releases](https://github.com/static-north-dev/valiant/releases) · [Contributing](CONTRIBUTING.md)

## Local verification

Install the exact SDK declared in `global.json` before running repository
commands. SDK roll-forward is disabled because SDK-provided packages participate
in the committed NuGet lock graph.

```bash
dotnet restore StaticNorth.Valiant.sln --locked-mode
dotnet build StaticNorth.Valiant.sln --configuration Debug --no-restore
dotnet test --solution StaticNorth.Valiant.sln --configuration Debug --no-build
dotnet build StaticNorth.Valiant.sln --configuration Release --no-restore
dotnet test --solution StaticNorth.Valiant.sln --configuration Release --no-build
dotnet run --project validation/samples/StaticNorth.Valiant.Validation.Samples
dotnet run --project validation/samples/StaticNorth.Valiant.Validation.AspNetCore.Samples
dotnet msbuild StaticNorth.Valiant.Pack.proj -t:VerifyPackageConsumers -m:1 -p:Configuration=Release -p:NoBuild=true
dotnet msbuild StaticNorth.Valiant.Pack.proj -t:VerifyAotSmoke -m:1 -p:Configuration=Release -p:NoBuild=true -p:AotRuntimeIdentifier=osx-arm64
```

Repository-wide build, analyzer, package metadata, and dependency versions live in
`Directory.Build.props`, `Directory.Build.targets`, and `Directory.Packages.props`.
Every solution project commits `packages.lock.json`; after changing
`Directory.Packages.props` or the SDK version in `global.json`, regenerate the
graph with:

```bash
dotnet restore StaticNorth.Valiant.sln --force-evaluate
```

`StaticNorth.Valiant.Pack.proj` is the authoritative ten-package release manifest. Its
verification targets pack serially, inspect NuGet and symbol-package contents,
and run leaf-only applications that restore the produced packages from the
local feed. `StaticNorth.Valiant.Endpoints` is an internal contract assembly bundled inside
`StaticNorth.Valiant.Endpoints.AspNetCore`; it is not a standalone package.

## Contributing and security

Contributions target `main` through pull requests. See [CONTRIBUTING.md](CONTRIBUTING.md)
for development and verification guidance and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
for community expectations.

Do not report vulnerabilities in public issues. Follow [SECURITY.md](SECURITY.md)
to submit a private report.
