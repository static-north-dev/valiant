using StaticNorth.Valiant.Endpoints.AspNetCore;
using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;
using StaticNorth.Valiant.Endpoints.AspNetCore.TestApp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<BindingSourceService>();

builder.Services.AddValiantEndpoints(options =>
{
    options.QueryParameters.EnableBinding = true;
    options.QueryParameters.Parsing = new QueryParsingOptions
    {
        SupportedArrayFormats = [QueryArrayFormat.RepeatedKeys],
        SupportedObjectFormats = [QueryObjectFormat.DotNotation],
        EnableJsonQueryValues = false
    };
});

var app = builder.Build();

app.MapValiantEndpoints();

app.Run();
