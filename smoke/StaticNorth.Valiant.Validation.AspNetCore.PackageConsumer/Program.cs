using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StaticNorth.Valiant.Validation;
using StaticNorth.Valiant.Validation.AspNetCore;
using StaticNorth.Valiant.Validation.DependencyInjection;
using StaticNorth.Valiant.Validation.Infrastructure;

var result = new SmokeRequestValidator(new ValiantValidationOptions
{
    Mode = ValiantValidationMode.Fast,
}).Validate(new SmokeRequest(""));

if (result.IsValid)
{
    throw new InvalidOperationException("The packaged validation generator did not execute its fast rules.");
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddValiantValidators();
builder.Services.AddValiantMvcValidation();
builder.Services.AddSingleton(new SmokeRequest(""));
await using var app = builder.Build();
var handlerReached = false;
app.MapGet(
        "/validate",
        ([FromServices] SmokeRequest request) =>
        {
            handlerReached = true;
            return Results.Ok(request);
        })
    .WithValiantValidation();

var endpoint = ((IEndpointRouteBuilder)app).DataSources
    .SelectMany(static dataSource => dataSource.Endpoints)
    .Single();
using var scope = app.Services.CreateScope();
var httpContext = new DefaultHttpContext
{
    RequestServices = scope.ServiceProvider,
    Response =
    {
        Body = new MemoryStream()
    }
};
await endpoint.RequestDelegate!(httpContext);

var invokers = scope.ServiceProvider.GetServices<IValiantValidatorInvoker>();
if (httpContext.Response.StatusCode != StatusCodes.Status400BadRequest ||
    handlerReached ||
    invokers.Count(static invoker => invoker.ModelType == typeof(SmokeRequest)) != 1)
{
    throw new InvalidOperationException("The packaged generated Minimal API validation path did not execute.");
}

Console.WriteLine("StaticNorth.Valiant.Validation.AspNetCore package consumer passed.");

public sealed record SmokeRequest(string? Name);

[ValiantValidator]
public sealed partial class SmokeRequestValidator : ValidatorDefinition<SmokeRequest>
{
    public override void Configure(ValidationContext<SmokeRequest> context)
    {
        context.Property(static request => request.Name).Required();
    }
}
