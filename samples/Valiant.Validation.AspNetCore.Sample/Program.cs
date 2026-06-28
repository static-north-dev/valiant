using Valiant.Validation.AspNetCore.Sample.Api;
using Valiant.Validation.AspNetCore.Sample.Scenarios;

if (!args.Contains("--run", StringComparer.OrdinalIgnoreCase))
{
    var builder = WebApplication.CreateBuilder(args);
    ValiantAspNetCoreSetup.ConfigureServices(builder.Services);

    var app = builder.Build();
    ValiantAspNetCoreSetup.MapEndpoints(app);

    await app.RunAsync();
    return;
}

await AspNetCoreSample.RunAsync();
