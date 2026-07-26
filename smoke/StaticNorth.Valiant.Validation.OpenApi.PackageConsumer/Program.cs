using StaticNorth.Valiant.Validation;
using StaticNorth.Valiant.Validation.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddValiantOpenApi();

if (new OpenApiSmokeValidator().Validate(new OpenApiSmokeRequest("")).IsValid)
{
    throw new InvalidOperationException("The OpenAPI leaf package did not forward the validation analyzer.");
}

Console.WriteLine("StaticNorth.Valiant.Validation.AspNetCore.OpenApi package consumer passed.");

public sealed record OpenApiSmokeRequest(string? Name);

[ValiantValidator]
public sealed partial class OpenApiSmokeValidator : ValidatorDefinition<OpenApiSmokeRequest>
{
    public override void Configure(ValidationContext<OpenApiSmokeRequest> context)
    {
        context.Property(static request => request.Name).Required();
    }
}
