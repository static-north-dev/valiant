using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Valiant.AspNetCore;

namespace Valiant.Tests;

public sealed class AspNetCoreIntegrationTests
{
    [Test]
    public async Task ProblemDetails_Groups_Failures_By_Property_Name()
    {
        var result = new ValidationResult(
        [
            new ValidationFailure("Name", "Required", "Name is required."),
            new ValidationFailure("Name", "MaxLength", "Name is too long."),
            new ValidationFailure("Address.Zip", "Required", "Zip is required.")
        ]);

        var errors = result.ToValidationProblemDictionary();
        var httpProblemDetails = result.ToHttpValidationProblemDetails();
        var mvcProblemDetails = result.ToMvcValidationProblemDetails();

        await Assert.That(errors["Name"]).IsEquivalentTo(["Name is required.", "Name is too long."]);
        await Assert.That(errors["Address.Zip"]).IsEquivalentTo(["Zip is required."]);
        await Assert.That(httpProblemDetails.Errors["Name"]).IsEquivalentTo(["Name is required.", "Name is too long."]);
        await Assert.That(mvcProblemDetails.Errors["Address.Zip"]).IsEquivalentTo(["Zip is required."]);
    }

    [Test]
    public async Task AddValiantMvcValidation_Adds_Mvc_Model_Validator_Provider()
    {
        var services = new ServiceCollection();

        services.AddValiantMvcValidation();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MvcOptions>>().Value;

        await Assert.That(options.ModelValidatorProviders.Any(static x => x.GetType().Name == "ValiantModelValidatorProvider")).IsTrue();
    }

    [Test]
    public async Task Minimal_Api_Validation_Extensions_Can_Be_Applied_To_Routes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IValidator<AspNetUser>, AspNetUserValidator>();
        var app = builder.Build();

        app.MapPost("/generic", static (AspNetUser user) => Results.Ok(user.Name))
            .WithValiantValidation<AspNetUser>();
        app.MapPost("/all", static (AspNetUser user) => Results.Ok(user.Name))
            .WithValiantValidation();

        await Assert.That(app).IsNotNull();
    }
}

public sealed class AspNetUser
{
    public string? Name { get; init; }
}

public sealed class AspNetUserValidator : IValidator<AspNetUser>
{
    public ValidationResult Validate(AspNetUser instance)
    {
        return string.IsNullOrWhiteSpace(instance.Name)
            ? new ValidationResult([new ValidationFailure("Name", "Required", "Name is required.")])
            : ValidationResult.Valid;
    }

    public ValueTask<ValidationResult> ValidateAsync(AspNetUser instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<ValidationResult>(Validate(instance));
    }

    public bool IsValid(AspNetUser instance)
    {
        return Validate(instance).IsValid;
    }

    public ValueTask<bool> IsValidAsync(AspNetUser instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(IsValid(instance));
    }
}
