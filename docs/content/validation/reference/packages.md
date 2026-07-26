---
title: Package guide
description: Choose the smallest Valiant Validation package set for your application.
---

# Package guide

:::caution Breaking migration from Valiant
Replace each former `Valiant.*` package reference and `using Valiant...`
directive with its `StaticNorth.Valiant.*` counterpart. Public C# namespaces
now use `StaticNorth.Valiant.*`, so application source must be updated too.
:::

Install the most specific package for each integration you use. Integration packages carry the core validation runtime, build, and analyzer assets, so a separate `StaticNorth.Valiant.Validation` reference is unnecessary.

| Package | Use it for | Target |
| --- | --- | --- |
| `StaticNorth.Valiant.Validation` | Core validators without a host integration | .NET 8 and .NET Standard 2.0 |
| `StaticNorth.Valiant.Validation.AspNetCore` | Minimal APIs, MVC, and problem details | .NET 8 |
| `StaticNorth.Valiant.Validation.AspNetCore.OpenApi` | Native ASP.NET Core OpenAPI schema rules | .NET 10 |
| `StaticNorth.Valiant.Validation.MediatR` | MediatR 12.5 pipeline validation | .NET 8 |
| `StaticNorth.Valiant.Validation.Mediator` | Source-generated Mediator pipeline validation | .NET 8 |

## Common combinations

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
