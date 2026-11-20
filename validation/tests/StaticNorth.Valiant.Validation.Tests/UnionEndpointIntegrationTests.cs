using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using StaticNorth.Valiant.Endpoints;
using StaticNorth.Valiant.Endpoints.AspNetCore;
using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;
using StaticNorth.Valiant.Validation.AspNetCore;
using StaticNorth.Valiant.Validation.AspNetCore.OpenApi;

namespace StaticNorth.Valiant.Validation.Tests;

public sealed class UnionEndpointIntegrationTests
{
    [Test]
    public async Task Generated_Endpoint_Binds_Union_Cases_And_Describes_Them_In_OpenApi()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddValiantEndpoints(typeof(UnionEndpoint).Assembly);
        builder.Services.AddSingleton<IValiantValidationMetadataProvider, __ValiantStaticNorth_Valiant_Validation_Tests_UnionEndpointModelValidatorMetadataProvider>();
        builder.Services.AddOpenApi(options => options.AddValiantValidationRules());

        await using var app = builder.Build();
        app.MapValiantEndpoints();
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var text = new StringContent("\"ready\"", Encoding.UTF8, "application/json");
        using var textResponse = await client.PostAsync("/union", text);
        await Assert.That(textResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await textResponse.Content.ReadAsStringAsync()).IsEqualTo("\"ready\"");

        using var model = new StringContent("{\"name\":\"ready\"}", Encoding.UTF8, "application/json");
        using var modelResponse = await client.PostAsync("/union", model);
        await Assert.That(modelResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var modelJson = JsonDocument.Parse(await modelResponse.Content.ReadAsStringAsync());
        await Assert.That(modelJson.RootElement.GetProperty("name").GetString()).IsEqualTo("ready");

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty("/union").GetProperty("post");
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var requestSchema = operation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var responseSchema = operation.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        foreach (var schema in new[] { requestSchema, responseSchema })
        {
            var cases = ResolveSchema(schema, schemas).GetProperty("anyOf").EnumerateArray()
                .Select(item => ResolveSchema(item, schemas))
                .ToArray();
            await Assert.That(cases.Length).IsEqualTo(2);
            var modelSchema = cases.Single(static item => item.TryGetProperty("properties", out _));
            var name = modelSchema.GetProperty("properties").GetProperty("name");
            await Assert.That(name.GetProperty("minLength").GetInt32()).IsEqualTo(1);
            await Assert.That(name.GetProperty("x-valiant-rules").GetArrayLength()).IsEqualTo(1);
        }
    }

    private static JsonElement ResolveSchema(JsonElement schema, JsonElement schemas)
    {
        return schema.TryGetProperty("$ref", out var reference)
            ? schemas.GetProperty(reference.GetString()!.Split('/')[^1])
            : schema;
    }
}
