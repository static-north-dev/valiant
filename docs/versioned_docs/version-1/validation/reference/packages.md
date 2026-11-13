---
title: Packages and compatibility
description: Choose Valiant packages and check the build SDK, target frameworks, and Native AOT paths.
---

# Packages and compatibility

Install the most specific package for each integration you use. Integration packages carry the core validation runtime, build, and analyzer assets, so a separate `StaticNorth.Valiant.Validation` reference is unnecessary.

| Package | Use it for | Target |
| --- | --- | --- |
| `StaticNorth.Valiant.Validation` | Core validators without a host integration | .NET 8 and .NET Standard 2.0 |
| `StaticNorth.Valiant.Validation.AspNetCore` | Minimal APIs, MVC, and problem details | .NET 8 |
| `StaticNorth.Valiant.Validation.AspNetCore.OpenApi` | Native ASP.NET Core OpenAPI schema rules | .NET 10 |
| `StaticNorth.Valiant.Validation.MediatR` | MediatR 12.5 pipeline validation | .NET 8 |
| `StaticNorth.Valiant.Validation.Mediator` | Source-generated Mediator pipeline validation | .NET 8 |

## Build SDK and target frameworks

Valiant 1.2.1 generators require the .NET 10.0.400 SDK or newer. The table lists the framework assets shipped by each package. You can use the newer SDK to compile a project targeting `net8.0`. The OpenAPI integration ships a `net10.0` asset.

Run `dotnet --version` in the project directory. If you see `CS9057` or generated methods are missing, check whether a `global.json` is selecting an older SDK.

## Endpoints, Mapping, and Settings

| Package | Use it for | Included framework assets |
| --- | --- | --- |
| `StaticNorth.Valiant.Endpoints.AspNetCore` | Endpoint classes, groups, and query binding | .NET 8 |
| `StaticNorth.Valiant.Mapping` | Generated object mapping | .NET 8 and .NET Standard 2.0 |
| `StaticNorth.Valiant.Settings` | Generated settings schemas and optional Options registration | .NET 8 and .NET Standard 2.0 |
| `StaticNorth.Valiant.Settings.AspNetCore` | Settings explorer and JSON endpoint | .NET 8 |
| `StaticNorth.Valiant.Settings.Validation` | Validate bound Options with Valiant | .NET 8 |

See the [1.2.1 release](https://github.com/static-north-dev/valiant/releases/tag/v1.2.1) and [NuGet packages](https://www.nuget.org/profiles/staticNorth). This guide covers the published 1.2.1 package set.

## Trimming and Native AOT

| Path | What to use |
| --- | --- |
| Validation | Use generated validators in `Fast` mode. For Minimal APIs, use generated DI with the parameterless filter, or the typed filter for manual registration. [ASP.NET Core guidance](../guides/aspnetcore-openapi.md#minimal-apis) |
| Validation in MVC | Use a regular ASP.NET Core MVC host. The Minimal API Native AOT guidance applies to Minimal API filters. |
| Endpoints | Pass the endpoint assemblies explicitly and leave Valiant query binding disabled. Use AOT-compatible ASP.NET Core handlers and serialization. [Native AOT registration](../../endpoints/reference/registration-generation.md#native-aot) |
| Mapping | Use `Fast` for direct generated assignments. `Debuggable` uses expression compilation and reflection. [Runtime modes](../../mapping/reference.md#runtime-options) |
| Settings | Generated schema lookup is exercised by the Native AOT smoke app. Generated Options registration delegates binding to Microsoft Options. Check binding, validation, and serialization separately for your host, including any explorer routes. [Schema API](../../settings/reference/schema-api.md) · [Options binding](../../settings/guides/binding-validation.md) |

## Common Validation combinations

```xml
<!-- Minimal API validation only -->
<PackageReference Include="StaticNorth.Valiant.Validation.AspNetCore" Version="..." />
```

```xml
<!-- Minimal API validation plus OpenAPI rule metadata -->
<PackageReference Include="StaticNorth.Valiant.Validation.AspNetCore" Version="..." />
<PackageReference Include="StaticNorth.Valiant.Validation.AspNetCore.OpenApi" Version="..." />
```

```xml
<!-- Application-layer validation with MediatR -->
<PackageReference Include="StaticNorth.Valiant.Validation.MediatR" Version="..." />
```

Reference more than one integration package when the application uses more than one integration. Do not add the core package solely to make source generation work.
