using StaticNorth.Valiant.Validation.AspNetCore;
using StaticNorth.Valiant.Validation.AspNetCore.Samples.Orders;
using StaticNorth.Valiant.Validation.DependencyInjection;

namespace StaticNorth.Valiant.Validation.AspNetCore.Samples.MinimalApi;

/// <summary>
/// Demonstrates typed and all-parameter Minimal API validation filters.
/// Invalid requests are rejected before the endpoint handler runs.
/// </summary>
public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddValiantValidators(options =>
        {
            options.Mode = ValiantValidationMode.Fast;
            options.Lifetime = ValiantValidatorLifetime.Scoped;
            options.FailureBehavior = ValiantFailureBehavior.ValidateAll;
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/orders/typed", static (CreateOrder order, HttpContext context) =>
                {
                    context.Response.Headers["X-Handler-Reached"] = "true";
                    return Results.Created($"/orders/{order.CustomerEmail}", order);
                })
                .WithValiantValidation<CreateOrder>();

            endpoints.MapPost("/orders/all", static (CreateOrder order, HttpContext context) =>
                {
                    context.Response.Headers["X-Handler-Reached"] = "true";
                    return Results.Ok(order);
                })
                .WithValiantValidation();
        });
    }
}
