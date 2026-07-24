using Microsoft.AspNetCore.Routing;
using Valiant.Endpoints;
using Valiant.Endpoints.AspNetCore;
using Valiant.Endpoints.AspNetCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddValiantEndpoints();

var app = builder.Build();
app.MapValiantEndpoints();

var routes = ((IEndpointRouteBuilder)app).DataSources
    .SelectMany(static source => source.Endpoints)
    .OfType<RouteEndpoint>()
    .Select(static endpoint => endpoint.RoutePattern.RawText)
    .ToArray();

if (!routes.Contains("/package-smoke", StringComparer.Ordinal))
{
    throw new InvalidOperationException("The packaged endpoint generator did not map its generated route.");
}

Console.WriteLine("Valiant.Endpoints.AspNetCore package consumer passed.");

[ValiantEndpoint]
public sealed class PackageSmokeEndpoint : IEndpoint
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
    {
        return builder.MapGet("/package-smoke", static () => Results.Ok());
    }
}
