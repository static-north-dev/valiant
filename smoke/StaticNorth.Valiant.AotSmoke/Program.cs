using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StaticNorth.Valiant.Endpoints.AspNetCore;
using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;
using StaticNorth.Valiant.Mapping;
using StaticNorth.Valiant.Settings;
using StaticNorth.Valiant.Validation;
using StaticNorth.Valiant.Validation.AspNetCore;
using StaticNorth.Valiant.Validation.DependencyInjection;

var mapped = new AotMapper(new ValiantMappingOptions
{
    Mode = ValiantMappingMode.Fast,
}).Map(new AotSource("native"));

var validation = new AotValidator(new ValiantValidationOptions
{
    Mode = ValiantValidationMode.Fast,
}).Validate(new AotRequest(""));

var schema = ValiantSettingsSchemas.Get<AotSettings>().Schema;

var builder = WebApplication.CreateSlimBuilder();
builder.Services.AddValiantValidators(options => options.Mode = ValiantValidationMode.Fast);
builder.Services.AddSingleton(new AotRequest(""));
builder.Services.AddValiantEndpoints(typeof(Program).Assembly);

await using var app = builder.Build();
var handlerReached = false;
app.MapGet(
        "/validation",
        ([FromServices] AotRequest request) =>
        {
            handlerReached = true;
            return Results.Ok(request);
        })
    .WithValiantValidation();

app.MapValiantEndpoints();

var endpoint = ((IEndpointRouteBuilder)app).DataSources
    .SelectMany(static dataSource => dataSource.Endpoints)
    .OfType<RouteEndpoint>()
    .Single(static endpoint => endpoint.RoutePattern.RawText == "/validation");
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

if (mapped.Value != "native" ||
    validation.IsValid ||
    schema.SectionPath != "Aot" ||
    httpContext.Response.StatusCode != StatusCodes.Status400BadRequest ||
    handlerReached ||
    !await ExerciseGeneratedRouteAsync(app, "/generated", "standalone") ||
    !await ExerciseGeneratedRouteAsync(app, "/aot/generated", "grouped"))
{
    return 1;
}

Console.WriteLine("Valiant Native AOT package smoke passed.");
return 0;

static async Task<bool> ExerciseGeneratedRouteAsync(WebApplication app, string route, string expected)
{
    var endpoint = ((IEndpointRouteBuilder)app).DataSources
        .SelectMany(static source => source.Endpoints)
        .OfType<RouteEndpoint>()
        .Single(candidate => candidate.RoutePattern.RawText == route);
    using var body = new MemoryStream();
    var context = new DefaultHttpContext
    {
        RequestServices = app.Services,
        Response = { Body = body }
    };

    await endpoint.RequestDelegate!(context);
    return context.Response.StatusCode == StatusCodes.Status200OK && Encoding.UTF8.GetString(body.ToArray()) == expected;
}

public sealed record AotSource(string Value);

public sealed record AotTarget(string? Value);

[ValiantMapper]
public sealed partial class AotMapper : MapperDefinition<AotSource, AotTarget>
{
    public override void Configure(MappingContext<AotSource, AotTarget> context)
    {
        context.Map(source => source.Value, target => target.Value);
    }
}

public sealed record AotRequest(string? Name);

[ValiantValidator]
public sealed partial class AotValidator : ValidatorDefinition<AotRequest>
{
    public override void Configure(ValidationContext<AotRequest> context)
    {
        context.Property(static request => request.Name).Required();
    }
}

[ValiantSettings("Aot")]
public sealed record AotSettings
{
    public string? Value { get; init; }
}
