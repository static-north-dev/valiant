using Valiant.Validation.AspNetCore.OpenApi;
using Valiant.Validation.AspNetCore.Samples.Orders;
using Valiant.Validation.DependencyInjection;

namespace Valiant.Validation.AspNetCore.Samples.OpenApi;

/// <summary>
/// Demonstrates projecting generated Valiant validation metadata into the
/// application's real OpenAPI document.
/// </summary>
public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddOpenApi();
        services.AddValiantValidators();
        services.AddValiantOpenApi();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapOpenApi();
            endpoints.MapPost("/orders", static (CreateOrder order) => Results.Ok(order))
                .WithValiantValidation<CreateOrder>();
        });
    }
}
