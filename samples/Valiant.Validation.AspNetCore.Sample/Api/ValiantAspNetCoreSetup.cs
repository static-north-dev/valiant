using Valiant.Validation.AspNetCore.Sample.Models;
using Valiant.Validation.AspNetCore.OpenApi;
using Valiant.Validation.DependencyInjection;

namespace Valiant.Validation.AspNetCore.Sample.Api;

internal static class ValiantAspNetCoreSetup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddOpenApi();
        services.AddValiantValidations(options =>
        {
            options.Mode = ValiantValidationMode.Fast;
            options.Lifetime = ValiantValidatorLifetime.Scoped;
            options.FailureBehavior = ValiantFailureBehavior.ValidateAll;
        });
        services.AddValiantOpenApi();
        services
            .AddControllers()
            .AddValiantValidation();
    }

    public static void MapEndpoints(WebApplication app)
    {
        app.MapOpenApi();
        app.MapControllers();

        app.MapPost("/orders/typed", static (CreateOrder order) => Results.Created($"/orders/{order.CustomerEmail}", order))
            .WithValiantValidation<CreateOrder>();

        app.MapPost("/orders/all", static (CreateOrder order) => Results.Ok(order))
            .WithValiantValidation();

        app.MapPost("/orders/problem", static (CreateOrder order, IValidator<CreateOrder> validator) =>
        {
            var result = validator.Validate(order);
            return result.IsValid
                ? Results.Ok(order)
                : Results.ValidationProblem(result.ToValidationProblemDictionary());
        });
    }
}
