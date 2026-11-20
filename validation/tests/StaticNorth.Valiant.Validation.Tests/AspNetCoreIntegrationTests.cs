using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StaticNorth.Valiant.Validation.AspNetCore;
using StaticNorth.Valiant.Validation.Infrastructure;

namespace StaticNorth.Valiant.Validation.Tests;

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

    [Test]
    public async Task Generated_Validation_Plans_Are_Shared_Only_Within_The_Same_Host()
    {
        var resolutions = 0;
        for (var host = 1; host <= 2; host++)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddTransient<IValiantValidatorInvoker>(_ =>
            {
                resolutions++;
                return new ValiantValidatorInvoker<string>();
            });

            await using var app = builder.Build();
            app.MapGet("/first", (string value) => value).WithValiantValidation();
            app.MapGet("/second", (string value) => value).WithValiantValidation();

            var endpoints = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(static source => source.Endpoints).ToArray();
            await Assert.That(endpoints.Length).IsEqualTo(2);
            await Assert.That(resolutions).IsEqualTo(host);
        }
    }

    [Test]
    public async Task Minimal_Api_Validation_Extensions_Add_OpenApi_Validation_Problem_Metadata()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IValidator<AspNetUser>, AspNetUserValidator>();
        var app = builder.Build();

        app.MapPost("/generic", static (AspNetUser user) => Results.Ok(user.Name))
            .WithValiantValidation<AspNetUser>();
        app.MapPost("/all", static (AspNetUser user) => Results.Ok(user.Name))
            .WithValiantValidation();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .ToArray();

        await Assert.That(endpoints).Count().IsEqualTo(2);
        foreach (var endpoint in endpoints)
        {
            var produces = endpoint.Metadata.OfType<IProducesResponseTypeMetadata>().ToArray();
            await Assert.That(produces.Any(static metadata =>
                metadata.StatusCode == StatusCodes.Status400BadRequest &&
                metadata.Type == typeof(HttpValidationProblemDetails))).IsTrue();
        }

    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Minimal_Api_Validation_Skips_Validators_When_Parameter_Binding_Fails(bool useTypedOverload)
    {
        var probe = new OrderedValidationProbe();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.Configure<RouteHandlerOptions>(static options => options.ThrowOnBadRequest = false);
        builder.Services.AddSingleton(probe);
        builder.Services.AddTransient<IValidator<string>, FirstOrderedStringValidator>();

        await using var app = builder.Build();
        var route = app.MapGet(
            "/binding",
            static (string value, int count, HttpContext context) =>
            {
                context.Response.Headers["X-Handler-Reached"] = "true";
                return Results.Ok();
            });
        var validatedRoute = useTypedOverload
            ? route.WithValiantValidation<string>()
            : route.WithValiantValidation();
        validatedRoute.AddEndpointFilter(static (context, next) =>
        {
            context.HttpContext.Response.Headers["X-Next-Filter-Reached"] = "true";
            return next(context);
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var bindingFailure = await client.GetAsync("/binding?value=example&count=invalid");

        await Assert.That(bindingFailure.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(probe.FirstStringValidatorConstructions).IsEqualTo(0);
        await Assert.That(probe.Events).IsEmpty();
        await Assert.That(bindingFailure.Headers.Contains("X-Next-Filter-Reached")).IsTrue();
        await Assert.That(bindingFailure.Headers.Contains("X-Handler-Reached")).IsFalse();

        using var validationFailure = await client.GetAsync("/binding?value=example&count=1");

        await Assert.That(validationFailure.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(probe.FirstStringValidatorConstructions).IsEqualTo(1);
        await AssertOrderedEvents(probe.Events, "string-first:example");
    }

    [Test]
    public async Task Parameterless_Minimal_Api_Validation_Preserves_Argument_Order_And_Resolves_Validators_Once_Per_Type()
    {
        var probe = new OrderedValidationProbe();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(probe);
        builder.Services.AddTransient<IValidator<string>, FirstOrderedStringValidator>();
        builder.Services.AddTransient<IValidator<string>, SecondOrderedStringValidator>();
        builder.Services.AddTransient<IValidator<int>, OrderedIntValidator>();

        await using var app = builder.Build();
        app.MapGet(
                "/ordered",
                static (string first, int middle, string last, HttpContext context) =>
                {
                    context.Response.Headers["X-Handler-Reached"] = "true";
                    return Results.Ok();
                })
            .WithValiantValidation();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var firstResponse = await client.GetAsync("/ordered?first=first&middle=1&last=last");
        using var document = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var messages = document.RootElement.GetProperty("errors").GetProperty("Value");

        await Assert.That(firstResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(firstResponse.Headers.Contains("X-Handler-Reached")).IsFalse();
        await Assert.That(messages.GetArrayLength()).IsEqualTo(5);
        await Assert.That(messages[0].GetString()).IsEqualTo("string-first:first");
        await Assert.That(messages[1].GetString()).IsEqualTo("string-second:first");
        await Assert.That(messages[2].GetString()).IsEqualTo("int:1");
        await Assert.That(messages[3].GetString()).IsEqualTo("string-first:last");
        await Assert.That(messages[4].GetString()).IsEqualTo("string-second:last");
        await AssertOrderedEvents(
            probe.Events,
            "string-first:first",
            "string-second:first",
            "int:1",
            "string-first:last",
            "string-second:last");
        await Assert.That(probe.FirstStringValidatorConstructions).IsEqualTo(1);
        await Assert.That(probe.SecondStringValidatorConstructions).IsEqualTo(1);
        await Assert.That(probe.IntValidatorConstructions).IsEqualTo(1);

        probe.Events.Clear();
        var secondResponse = await client.GetAsync("/ordered?first=first&middle=1&last=last");

        await Assert.That(secondResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await AssertOrderedEvents(
            probe.Events,
            "string-first:first",
            "string-second:first",
            "int:1",
            "string-first:last",
            "string-second:last");
        await Assert.That(probe.FirstStringValidatorConstructions).IsEqualTo(2);
        await Assert.That(probe.SecondStringValidatorConstructions).IsEqualTo(2);
        await Assert.That(probe.IntValidatorConstructions).IsEqualTo(2);
    }

    [Test]
    public async Task Parameterless_Minimal_Api_Validation_Uses_Declared_Type_While_Typed_Validation_Uses_Runtime_Assignability()
    {
        var probe = new DeclaredTypeValidationProbe();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<DeclaredEndpointModel>(new DerivedEndpointModel());
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<IValidator<DeclaredEndpointModel>, DeclaredEndpointModelValidator>();
        builder.Services.AddSingleton<IValidator<DerivedEndpointModel>, DerivedEndpointModelValidator>();

        await using var app = builder.Build();
        app.MapGet("/declared", static (DeclaredEndpointModel model) => Results.Ok(model.Value))
            .WithValiantValidation();
        app.MapGet("/runtime", static (DeclaredEndpointModel model) => Results.Ok(model.Value))
            .WithValiantValidation<DerivedEndpointModel>();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var declaredResponse = await client.GetAsync("/declared");

        await Assert.That(declaredResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(probe.DeclaredValidatorCalls).IsEqualTo(1);
        await Assert.That(probe.DerivedValidatorCalls).IsEqualTo(0);

        var runtimeResponse = await client.GetAsync("/runtime");

        await Assert.That(runtimeResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(probe.DeclaredValidatorCalls).IsEqualTo(1);
        await Assert.That(probe.DerivedValidatorCalls).IsEqualTo(1);
    }

    [Test]
    public async Task Parameterless_Minimal_Api_Validation_Can_Validate_Framework_Arguments_When_Their_Validator_Is_Registered()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IValidator<HttpContext>, HttpContextValidator>();

        await using var app = builder.Build();
        app.MapGet(
                "/context",
                static (HttpContext context) =>
                {
                    context.Response.Headers["X-Handler-Reached"] = "true";
                    return Results.Ok();
                })
            .WithValiantValidation();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/context");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(response.Headers.Contains("X-Handler-Reached")).IsFalse();
    }

    [Test]
    public async Task Parameterless_Minimal_Api_Validation_Validates_AsParameters_Wrapper()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IValidator<EndpointQuery>, EndpointQueryValidator>();

        await using var app = builder.Build();
        app.MapGet("/query", static ([AsParameters] EndpointQuery query) => Results.Ok(query.Name))
            .WithValiantValidation();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/query?name=");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Parameterless_Minimal_Api_Validation_Skips_Null_Optional_Arguments()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IValidator<AspNetUser>, AspNetUserValidator>();

        await using var app = builder.Build();
        app.MapPost("/nullable", static (AspNetUser? user) => Results.Ok(user))
            .WithValiantValidation();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsync("/nullable", content: null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Parameterless_Minimal_Api_Validation_Does_Not_Block_On_Warnings_Or_Info()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new AspNetUser());
        builder.Services.AddSingleton<IValidator<AspNetUser>, NonBlockingAspNetUserValidator>();

        await using var app = builder.Build();
        app.MapGet(
                "/severity",
                static (AspNetUser user, HttpContext context) =>
                {
                    context.Response.Headers["X-Handler-Reached"] = "true";
                    return Results.Ok(user);
                })
            .WithValiantValidation();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/severity");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Headers.GetValues("X-Handler-Reached").Single()).IsEqualTo("true");
    }

    private static async Task AssertOrderedEvents(List<string> actual, params string[] expected)
    {
        await Assert.That(actual.Count).IsEqualTo(expected.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            await Assert.That(actual[index]).IsEqualTo(expected[index]);
        }
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

public sealed class NonBlockingAspNetUserValidator : IValidator<AspNetUser>
{
    private static readonly ValidationResult Result = new(
    [
        new ValidationFailure("Name", "Warning", "warning", ValiantSeverity.Warning),
        new ValidationFailure("Name", "Info", "info", ValiantSeverity.Info)
    ]);

    public ValidationResult Validate(AspNetUser instance)
    {
        return Result;
    }

    public ValueTask<ValidationResult> ValidateAsync(
        AspNetUser instance,
        CancellationToken cancellationToken = default)
    {
        return new ValueTask<ValidationResult>(Result);
    }

    public bool IsValid(AspNetUser instance)
    {
        return true;
    }

    public ValueTask<bool> IsValidAsync(
        AspNetUser instance,
        CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(true);
    }
}

public sealed class OrderedValidationProbe
{
    public List<string> Events { get; } = [];

    public int FirstStringValidatorConstructions { get; set; }

    public int SecondStringValidatorConstructions { get; set; }

    public int IntValidatorConstructions { get; set; }
}

public sealed class FirstOrderedStringValidator : IValidator<string>
{
    private readonly OrderedValidationProbe _probe;

    public FirstOrderedStringValidator(OrderedValidationProbe probe)
    {
        _probe = probe;
        probe.FirstStringValidatorConstructions++;
    }

    public ValidationResult Validate(string instance)
    {
        _probe.Events.Add($"string-first:{instance}");
        return Invalid("string-first", instance);
    }

    public ValueTask<ValidationResult> ValidateAsync(string instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<ValidationResult>(Validate(instance));
    }

    public bool IsValid(string instance)
    {
        return Validate(instance).IsValid;
    }

    public ValueTask<bool> IsValidAsync(string instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(IsValid(instance));
    }

    private static ValidationResult Invalid(string validator, string value)
    {
        return new ValidationResult([new ValidationFailure("Value", "Invalid", $"{validator}:{value}")]);
    }
}

public sealed class SecondOrderedStringValidator : IValidator<string>
{
    private readonly OrderedValidationProbe _probe;

    public SecondOrderedStringValidator(OrderedValidationProbe probe)
    {
        _probe = probe;
        probe.SecondStringValidatorConstructions++;
    }

    public ValidationResult Validate(string instance)
    {
        _probe.Events.Add($"string-second:{instance}");
        return new ValidationResult([new ValidationFailure("Value", "Invalid", $"string-second:{instance}")]);
    }

    public ValueTask<ValidationResult> ValidateAsync(string instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<ValidationResult>(Validate(instance));
    }

    public bool IsValid(string instance)
    {
        return Validate(instance).IsValid;
    }

    public ValueTask<bool> IsValidAsync(string instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(IsValid(instance));
    }
}

public sealed class OrderedIntValidator : IValidator<int>
{
    private readonly OrderedValidationProbe _probe;

    public OrderedIntValidator(OrderedValidationProbe probe)
    {
        _probe = probe;
        probe.IntValidatorConstructions++;
    }

    public ValidationResult Validate(int instance)
    {
        _probe.Events.Add($"int:{instance}");
        return new ValidationResult([new ValidationFailure("Value", "Invalid", $"int:{instance}")]);
    }

    public ValueTask<ValidationResult> ValidateAsync(int instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<ValidationResult>(Validate(instance));
    }

    public bool IsValid(int instance)
    {
        return Validate(instance).IsValid;
    }

    public ValueTask<bool> IsValidAsync(int instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(IsValid(instance));
    }
}

public class DeclaredEndpointModel
{
    public string Value { get; init; } = "declared";
}

public sealed class DerivedEndpointModel : DeclaredEndpointModel;

public sealed class DeclaredTypeValidationProbe
{
    public int DeclaredValidatorCalls { get; set; }

    public int DerivedValidatorCalls { get; set; }
}

public sealed class DeclaredEndpointModelValidator(DeclaredTypeValidationProbe probe) : IValidator<DeclaredEndpointModel>
{
    public ValidationResult Validate(DeclaredEndpointModel instance)
    {
        probe.DeclaredValidatorCalls++;
        return ValidationResult.Valid;
    }

    public ValueTask<ValidationResult> ValidateAsync(DeclaredEndpointModel instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<ValidationResult>(Validate(instance));
    }

    public bool IsValid(DeclaredEndpointModel instance)
    {
        return Validate(instance).IsValid;
    }

    public ValueTask<bool> IsValidAsync(DeclaredEndpointModel instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(IsValid(instance));
    }
}

public sealed class DerivedEndpointModelValidator(DeclaredTypeValidationProbe probe) : IValidator<DerivedEndpointModel>
{
    public ValidationResult Validate(DerivedEndpointModel instance)
    {
        probe.DerivedValidatorCalls++;
        return ValidationResult.Valid;
    }

    public ValueTask<ValidationResult> ValidateAsync(DerivedEndpointModel instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<ValidationResult>(Validate(instance));
    }

    public bool IsValid(DerivedEndpointModel instance)
    {
        return Validate(instance).IsValid;
    }

    public ValueTask<bool> IsValidAsync(DerivedEndpointModel instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(IsValid(instance));
    }
}

public sealed class HttpContextValidator : IValidator<HttpContext>
{
    public ValidationResult Validate(HttpContext instance)
    {
        return new ValidationResult([new ValidationFailure("Request", "Invalid", "The request is invalid.")]);
    }

    public ValueTask<ValidationResult> ValidateAsync(HttpContext instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<ValidationResult>(Validate(instance));
    }

    public bool IsValid(HttpContext instance)
    {
        return Validate(instance).IsValid;
    }

    public ValueTask<bool> IsValidAsync(HttpContext instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(IsValid(instance));
    }
}

public sealed class EndpointQuery
{
    public string? Name { get; init; }
}

public sealed class EndpointQueryValidator : IValidator<EndpointQuery>
{
    public ValidationResult Validate(EndpointQuery instance)
    {
        return string.IsNullOrWhiteSpace(instance.Name)
            ? new ValidationResult([new ValidationFailure("Name", "Required", "Name is required.")])
            : ValidationResult.Valid;
    }

    public ValueTask<ValidationResult> ValidateAsync(EndpointQuery instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<ValidationResult>(Validate(instance));
    }

    public bool IsValid(EndpointQuery instance)
    {
        return Validate(instance).IsValid;
    }

    public ValueTask<bool> IsValidAsync(EndpointQuery instance, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(IsValid(instance));
    }
}
