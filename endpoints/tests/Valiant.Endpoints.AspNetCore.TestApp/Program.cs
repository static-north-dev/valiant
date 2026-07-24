using Valiant.Endpoints.AspNetCore;
using Valiant.Endpoints.AspNetCore.DependencyInjection;
using Valiant.Endpoints.AspNetCore.TestApp;

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
