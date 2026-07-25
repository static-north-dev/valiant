using Valiant.Validation;
using Valiant.Validation.AspNetCore;
using Valiant.Validation.DependencyInjection;

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
var app = builder.Build();
app.MapPost("/validate", static (SmokeRequest request) => Results.Ok(request))
    .WithValiantValidation<SmokeRequest>();

Console.WriteLine("Valiant.Validation.AspNetCore package consumer passed.");

public sealed record SmokeRequest(string? Name);

[ValiantValidator]
public sealed partial class SmokeRequestValidator : ValidatorDefinition<SmokeRequest>
{
    public override void Configure(ValidationContext<SmokeRequest> context)
    {
        context.Property(static request => request.Name).Required();
    }
}
