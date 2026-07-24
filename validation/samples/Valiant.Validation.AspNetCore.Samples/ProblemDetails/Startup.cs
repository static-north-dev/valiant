using Valiant.Validation.AspNetCore;
using Valiant.Validation.AspNetCore.Samples.Orders;
using Valiant.Validation.DependencyInjection;

namespace Valiant.Validation.AspNetCore.Samples.ProblemDetails;

/// <summary>
/// Demonstrates manual validation and conversion of the same validation result
/// to HTTP and MVC validation problem-details responses.
/// </summary>
public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddValiantValidators();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/problem-details/http", static (
                CreateOrder order,
                IValidator<CreateOrder> validator) =>
            {
                var result = validator.Validate(order);
                return result.IsValid
                    ? Results.Ok(order)
                    : Results.BadRequest(result.ToHttpValidationProblemDetails());
            });

            endpoints.MapPost("/problem-details/mvc", static (
                CreateOrder order,
                IValidator<CreateOrder> validator) =>
            {
                var result = validator.Validate(order);
                return result.IsValid
                    ? Results.Ok(order)
                    : Results.BadRequest(result.ToMvcValidationProblemDetails());
            });
        });
    }
}
