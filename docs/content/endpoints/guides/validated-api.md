---
title: A validated Minimal API
description: Use all four Valiant modules in one endpoint that reads settings, validates a request, and maps a response.
---

# A validated Minimal API

Build a `POST /orders` endpoint. Endpoints registers the route, Settings binds the order limit, Validation checks the request against that limit, and Mapping creates the response.

Use the [.NET SDK listed in the package guide](../../validation/reference/packages.md) and create an ASP.NET Core project:

```bash
dotnet new web -n OrdersApi -f net8.0
cd OrdersApi
dotnet add package StaticNorth.Valiant.Endpoints.AspNetCore --version 1.2.1
dotnet add package StaticNorth.Valiant.Validation.AspNetCore --version 1.2.1
dotnet add package StaticNorth.Valiant.Mapping --version 1.2.1
dotnet add package StaticNorth.Valiant.Settings --version 1.2.1
```

Add the order limit to `appsettings.json`:

```json title="appsettings.json"
{
  "Orders": {
    "MaxQuantity": 5
  }
}
```

Declare the endpoint, validator, mapper, and settings model in `Orders.cs`:

```csharp title="Orders.cs"
using Microsoft.Extensions.Options;
using StaticNorth.Valiant.Endpoints;
using StaticNorth.Valiant.Endpoints.AspNetCore;
using StaticNorth.Valiant.Mapping;
using StaticNorth.Valiant.Settings;
using StaticNorth.Valiant.Validation;
using StaticNorth.Valiant.Validation.AspNetCore;

public sealed record CreateOrder(int ProductId, int Quantity);
public sealed record OrderResponse(int ProductId, int Quantity);

[ValiantSettings("Orders", configure: true)]
public sealed record OrderSettings
{
    public int MaxQuantity { get; init; } = 10;
}

[ValiantValidator]
public sealed partial class CreateOrderValidator(IOptions<OrderSettings> settings)
    : ValidatorDefinition<CreateOrder>
{
    public override void Configure(ValidationContext<CreateOrder> context)
    {
        context.Property(order => order.ProductId).GreaterThan(0);
        context.Property(order => order.Quantity)
            .GreaterThan(0)
            .LessThanOrEqualTo(settings.Value.MaxQuantity);
    }
}

[ValiantMapper]
public sealed partial class OrderResponseMapper
    : MapperDefinition<CreateOrder, OrderResponse>;

[ValiantEndpoint]
internal sealed class CreateOrderEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
        builder.MapPost("/orders", Handle).WithValiantValidation();

    private static IResult Handle(CreateOrder request, IMapper<CreateOrder, OrderResponse> mapper) =>
        Results.Ok(mapper.Map(request));
}
```

Replace `Program.cs` with the application setup:

```csharp title="Program.cs"
using StaticNorth.Valiant.Endpoints.AspNetCore;
using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;
using StaticNorth.Valiant.Mapping;
using StaticNorth.Valiant.Settings.DependencyInjection;
using StaticNorth.Valiant.Validation.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddValiantEndpoints();
builder.Services.AddValiantSettings(builder.Configuration);
builder.Services.AddValiantValidators();
builder.Services.AddValiantMappers();

var app = builder.Build();
app.MapValiantEndpoints();
app.Run();
```

Run `dotnet run --no-launch-profile --urls http://localhost:5075` and send a request:

```bash
curl -i http://localhost:5075/orders \
  -H 'Content-Type: application/json' \
  -d '{"productId":42,"quantity":2}'
```

The response is HTTP 200 with `{"productId":42,"quantity":2}`. Send a quantity of `6` to see an HTTP 400 validation problem with a `Quantity` error. The handler and mapper run only after validation succeeds.

This example uses a regular ASP.NET Core host. See the [deployment compatibility table](../../validation/reference/packages.md#trimming-and-native-aot) for the changes needed when publishing a native app.
