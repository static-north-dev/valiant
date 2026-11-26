using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StaticNorth.Valiant.Validation.AspNetCore;
using StaticNorth.Valiant.Validation.Infrastructure;

namespace StaticNorth.Valiant.Validation.Benchmarks;

internal sealed class RuntimeAsyncEndpoint : IAsyncDisposable
{
    internal const string RequestPath = "/validate/42";

    private readonly WebApplication _app;
    private volatile bool _rejectRequest;

    internal RuntimeAsyncEndpoint(bool nested, bool suspend)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(new ValiantValidationOptions { Mode = ValiantValidationMode.Fast });
        builder.Services.AddSingleton<IValiantValidatorInvoker, ValiantValidatorInvoker<RuntimeAsyncValidationInput>>();
        builder.Services.AddScoped(_ => new RuntimeAsyncValidationInput(
            new RuntimeAsyncRule(suspend, completeOnThreadPool: true, isValid: !_rejectRequest)));
        builder.Services.AddScoped<RuntimeAsyncSingleValidator>();
        if (nested)
        {
            builder.Services.AddScoped<IValidator<RuntimeAsyncValidationInput>, RuntimeAsyncNestedValidator>();
        }
        else
        {
            builder.Services.AddScoped<IValidator<RuntimeAsyncValidationInput>>(static services =>
                services.GetRequiredService<RuntimeAsyncSingleValidator>());
        }

        _app = builder.Build();
        _app.MapGet("/validate/{id:int}", static (int id, [FromServices] RuntimeAsyncValidationInput input) =>
                id > 0 && input.Rule is not null ? Results.NoContent() : Results.BadRequest())
            .WithValiantValidation();
    }

    internal Uri Address => new(_app.Urls.Single());

    internal async Task StartAsync()
    {
        await _app.StartAsync().ConfigureAwait(false);
        using var client = new HttpClient { BaseAddress = Address };
        using var validResponse = await client.GetAsync(RequestPath).ConfigureAwait(false);
        if (validResponse.StatusCode != HttpStatusCode.NoContent)
        {
            throw new InvalidOperationException("The benchmark request must pass validation.");
        }

        _rejectRequest = true;
        using var invalidResponse = await client.GetAsync(RequestPath).ConfigureAwait(false);
        if (invalidResponse.StatusCode != HttpStatusCode.BadRequest)
        {
            throw new InvalidOperationException("The endpoint must reject a request when its validation rule fails.");
        }

        _rejectRequest = false;
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
