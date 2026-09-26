using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using BasicStartup = StaticNorth.Valiant.Settings.AspNetCore.Samples.Basic.Startup;
using ConfigurationStartup = StaticNorth.Valiant.Settings.AspNetCore.Samples.Configuration.Startup;
using InvalidValidationStartup = StaticNorth.Valiant.Settings.AspNetCore.Samples.Validation.InvalidStartup;
using RequiredValidationStartup = StaticNorth.Valiant.Settings.AspNetCore.Samples.Validation.RequiredStartup;
using RuntimeValuesStartup = StaticNorth.Valiant.Settings.AspNetCore.Samples.RuntimeValues.Startup;
using SecretRevealStartup = StaticNorth.Valiant.Settings.AspNetCore.Samples.SecretReveal.Startup;
using ValidationStartup = StaticNorth.Valiant.Settings.AspNetCore.Samples.Validation.Startup;

namespace StaticNorth.Valiant.Settings.Tests.Samples;

public sealed class SettingsSamplesWebApplicationFactoryTests
{
    [Test]
    public async Task Basic_Sample_Maps_Default_Ui_And_Schema_Routes()
    {
        await using var factory = CreateFactory<BasicStartup>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var rootResponse = await client.GetAsync("/");
        var uiResponse = await client.GetAsync("/_valiant/settings");
        var schemasResponse = await client.GetAsync("/_valiant/settings/schemas");

        await Assert.That(rootResponse.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(rootResponse.Headers.Location?.OriginalString)
            .IsEqualTo("/_valiant/settings");
        await Assert.That(uiResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(schemasResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = await ReadJson(schemasResponse);
        var schema = FindSchema(document, "BasicSettings");
        await Assert.That(schema.GetProperty("sectionPath").GetString())
            .IsEqualTo("Samples:AspNetCore:Basic");
        await Assert.That(schema.TryGetProperty("runtimeValues", out _)).IsFalse();
    }

    [Test]
    public async Task Configuration_Sample_Uses_Custom_Path_And_Returned_Group_Authorization()
    {
        await using var factory = CreateFactory<ConfigurationStartup>();
        using var client = factory.CreateClient();

        var forbiddenResponse = await client.GetAsync(ConfigurationStartup.Path);
        var forbiddenSchemasResponse = await client.GetAsync(ConfigurationStartup.Path + "/schemas");
        using var request = new HttpRequestMessage(HttpMethod.Get, ConfigurationStartup.Path);
        request.Headers.Add(ConfigurationStartup.AdminHeader, "true");
        var allowedResponse = await client.SendAsync(request);
        using var schemasRequest = new HttpRequestMessage(
            HttpMethod.Get,
            ConfigurationStartup.Path + "/schemas");
        schemasRequest.Headers.Add(ConfigurationStartup.AdminHeader, "true");
        var allowedSchemasResponse = await client.SendAsync(schemasRequest);
        var defaultResponse = await client.GetAsync("/_valiant/settings");

        await Assert.That(forbiddenResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(forbiddenSchemasResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(allowedResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(allowedSchemasResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(defaultResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        using var document = await ReadJson(allowedSchemasResponse);
        _ = FindSchema(document, "ConfigurationSettings");
    }

    [Test]
    public async Task Runtime_Values_Sample_Masks_Explicit_And_Name_Matched_Secrets()
    {
        await using var factory = CreateFactory<RuntimeValuesStartup>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(RuntimeValuesStartup.Path + "/schemas?revealSecrets=true");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = await ReadJson(response);
        var schema = FindSchema(document, "RuntimeValueSettings");
        var values = schema.GetProperty("runtimeValues");

        await Assert.That(values.GetProperty("Name").GetString()).IsEqualTo("catalog");
        await Assert.That(values.GetProperty("ApiKey").GetString()).IsEqualTo("********");
        await Assert.That(values.GetProperty("AccessToken").GetString()).IsEqualTo("********");
        await Assert.That(document.RootElement.GetProperty("canRevealSecrets").GetBoolean()).IsFalse();
        await Assert.That(document.RootElement.GetProperty("secretsRevealed").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task Secret_Reveal_Sample_Requires_Explicit_Query_Opt_In()
    {
        await using var factory = CreateFactory<SecretRevealStartup>();
        using var client = factory.CreateClient();

        var maskedResponse = await client.GetAsync(SecretRevealStartup.Path + "/schemas");
        var revealedResponse = await client.GetAsync(SecretRevealStartup.Path + "/schemas?revealSecrets=true");

        await Assert.That(maskedResponse.Headers.CacheControl?.NoStore).IsTrue();
        await Assert.That(revealedResponse.Headers.CacheControl?.NoStore).IsTrue();
        await Assert.That(maskedResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(revealedResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var maskedDocument = await ReadJson(maskedResponse);
        using var revealedDocument = await ReadJson(revealedResponse);
        var maskedValues = FindSchema(maskedDocument, "SecretRevealSettings").GetProperty("runtimeValues");
        var revealedValues = FindSchema(revealedDocument, "SecretRevealSettings").GetProperty("runtimeValues");

        await Assert.That(maskedValues.GetProperty("ApiKey").GetString()).IsEqualTo("********");
        await Assert.That(revealedValues.GetProperty("ApiKey").GetString()).IsEqualTo("revealable-secret");
        await Assert.That(maskedDocument.RootElement.GetProperty("canRevealSecrets").GetBoolean()).IsTrue();
        await Assert.That(maskedDocument.RootElement.GetProperty("secretsRevealed").GetBoolean()).IsFalse();
        await Assert.That(revealedDocument.RootElement.GetProperty("canRevealSecrets").GetBoolean()).IsTrue();
        await Assert.That(revealedDocument.RootElement.GetProperty("secretsRevealed").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task Validation_Sample_Validates_And_Allows_A_Missing_Validator_By_Default()
    {
        await using var factory = CreateFactory<ValidationStartup>();
        using var client = factory.CreateClient();

        var validatedResponse = await client.GetAsync("/validation/valid");
        var missingValidatorResponse = await client.GetAsync("/validation/missing-validator");

        await Assert.That(validatedResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(missingValidatorResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var validatedDocument = await ReadJson(validatedResponse);
        using var missingValidatorDocument = await ReadJson(missingValidatorResponse);
        await Assert.That(validatedDocument.RootElement.GetProperty("name").GetString())
            .IsEqualTo("catalog");
        await Assert.That(validatedDocument.RootElement.GetProperty("retryCount").GetInt32())
            .IsEqualTo(3);
        await Assert.That(missingValidatorDocument.RootElement.GetProperty("enabled").GetBoolean())
            .IsTrue();
    }

    [Test]
    public async Task Invalid_Validation_Sample_Returns_Translated_Options_Failures()
    {
        await using var factory = CreateFactory<InvalidValidationStartup>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/validation/invalid");
        var content = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(content).Contains("Name");
        await Assert.That(content).Contains("RetryCount");
    }

    [Test]
    public async Task Required_Validation_Sample_Fails_When_A_Validator_Is_Missing()
    {
        await using var factory = CreateFactory<RequiredValidationStartup>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/validation/missing-validator");
        var content = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(content).Contains("No Valiant validator was registered");
    }

    private static WebApplicationFactory<TStartup> CreateFactory<TStartup>()
        where TStartup : class
    {
        return new SettingsSampleApplicationFactory<TStartup>();
    }

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static JsonElement FindSchema(JsonDocument document, string name)
    {
        return document.RootElement
            .GetProperty("schemas")
            .EnumerateArray()
            .Single(schema => schema.GetProperty("name").GetString() == name);
    }

    private sealed class SettingsSampleApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
        where TStartup : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseStartup<TStartup>();
        }
    }
}
