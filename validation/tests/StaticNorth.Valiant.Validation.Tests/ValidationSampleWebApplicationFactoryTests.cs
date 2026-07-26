using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using StaticNorth.Valiant.Validation.AspNetCore.Samples.Orders;

namespace StaticNorth.Valiant.Validation.Tests;

public sealed class ValidationSampleWebApplicationFactoryTests
{
    [Test]
    [Arguments("/orders/typed")]
    [Arguments("/orders/all")]
    public async Task MinimalApi_Sample_Rejects_Invalid_Requests_Before_Handler(string path)
    {
        await using var factory = new SampleApplicationFactory<
            global::StaticNorth.Valiant.Validation.AspNetCore.Samples.MinimalApi.Startup>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(path, InvalidOrder());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(response.Headers.Contains("X-Handler-Reached")).IsFalse();
        await Assert.That(errors.TryGetProperty("CustomerEmail", out _)).IsTrue();
        await Assert.That(errors.TryGetProperty("Lines", out _)).IsTrue();
        await Assert.That(errors.TryGetProperty("Total", out _)).IsTrue();
    }

    [Test]
    [Arguments("/orders/typed", HttpStatusCode.Created)]
    [Arguments("/orders/all", HttpStatusCode.OK)]
    public async Task MinimalApi_Sample_Allows_Valid_Requests_To_Reach_Handler(
        string path,
        HttpStatusCode expectedStatus)
    {
        await using var factory = new SampleApplicationFactory<
            global::StaticNorth.Valiant.Validation.AspNetCore.Samples.MinimalApi.Startup>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(path, ValidOrder());

        await Assert.That(response.StatusCode).IsEqualTo(expectedStatus);
        await Assert.That(response.Headers.GetValues("X-Handler-Reached").Single()).IsEqualTo("true");
    }

    [Test]
    [Arguments("/orders/typed")]
    [Arguments("/orders/all")]
    public async Task MinimalApi_Sample_Validates_Nested_Lines_And_Conditional_Vip_Rules(string path)
    {
        await using var factory = new SampleApplicationFactory<
            global::StaticNorth.Valiant.Validation.AspNetCore.Samples.MinimalApi.Startup>();
        using var client = factory.CreateClient();
        var order = ValidOrder() with
        {
            Lines =
            [
                new CreateOrderLine
                {
                    Sku = "",
                    Quantity = 0
                }
            ],
            IsVip = true,
            VipCode = null
        };

        var response = await client.PostAsJsonAsync(path, order);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(response.Headers.Contains("X-Handler-Reached")).IsFalse();
        await Assert.That(errors.TryGetProperty("Lines[0].Sku", out _)).IsTrue();
        await Assert.That(errors.TryGetProperty("Lines[0].Quantity", out _)).IsTrue();
        await Assert.That(errors.TryGetProperty("VipCode", out _)).IsTrue();
    }

    [Test]
    public async Task Mvc_Sample_Automatically_Rejects_Invalid_Model_And_Allows_Valid_Model()
    {
        await using var factory = new SampleApplicationFactory<
            global::StaticNorth.Valiant.Validation.AspNetCore.Samples.Mvc.Startup>();
        using var client = factory.CreateClient();

        var invalidResponse = await client.PostAsJsonAsync("/mvc/orders", InvalidOrder());
        var validResponse = await client.PostAsJsonAsync("/mvc/orders", ValidOrder());
        using var document = JsonDocument.Parse(await invalidResponse.Content.ReadAsStringAsync());

        await Assert.That(invalidResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(document.RootElement.GetProperty("errors").TryGetProperty("CustomerEmail", out _)).IsTrue();
        await Assert.That(validResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
    }

    [Test]
    [Arguments("/problem-details/http")]
    [Arguments("/problem-details/mvc")]
    public async Task ProblemDetails_Sample_Groups_Validation_Messages_By_Property(string path)
    {
        await using var factory = new SampleApplicationFactory<
            global::StaticNorth.Valiant.Validation.AspNetCore.Samples.ProblemDetails.Startup>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(path, InvalidOrder());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(errors.GetProperty("CustomerEmail")[0].GetString())
            .IsEqualTo("Customer email is required.");
        await Assert.That(errors.TryGetProperty("Lines", out _)).IsTrue();
        await Assert.That(errors.TryGetProperty("Total", out _)).IsTrue();
    }

    [Test]
    [Arguments("/problem-details/http")]
    [Arguments("/problem-details/mvc")]
    public async Task ProblemDetails_Sample_Returns_Success_For_A_Valid_Model(string path)
    {
        await using var factory = new SampleApplicationFactory<
            global::StaticNorth.Valiant.Validation.AspNetCore.Samples.ProblemDetails.Startup>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(path, ValidOrder());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task OpenApi_Sample_Projects_Validation_Metadata_Into_Real_Document()
    {
        await using var factory = new SampleApplicationFactory<
            global::StaticNorth.Valiant.Validation.AspNetCore.Samples.OpenApi.Startup>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schema = FindObjectSchema(document.RootElement, "customerEmail");
        var properties = schema.GetProperty("properties");
        var customerEmail = properties.GetProperty("customerEmail");
        var lines = properties.GetProperty("lines");
        var total = properties.GetProperty("total");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(schema.GetProperty("required").EnumerateArray()
            .Select(static item => item.GetString())).Contains("customerEmail");
        await Assert.That(customerEmail.GetProperty("minLength").GetInt32()).IsEqualTo(1);
        await Assert.That(customerEmail.GetProperty("maxLength").GetInt32()).IsEqualTo(80);
        await Assert.That(customerEmail.TryGetProperty("x-valiant-rules", out _)).IsTrue();
        await Assert.That(lines.GetProperty("minItems").GetInt32()).IsEqualTo(1);
        await Assert.That(lines.GetProperty("maxItems").GetInt32()).IsEqualTo(20);
        await Assert.That(total.GetProperty("minimum").GetDecimal()).IsEqualTo(0.01m);
        await Assert.That(total.GetProperty("maximum").GetDecimal()).IsEqualTo(100000m);
    }

    private static CreateOrder InvalidOrder()
    {
        return new CreateOrder
        {
            CustomerEmail = "",
            Lines = [],
            Total = 0
        };
    }

    private static CreateOrder ValidOrder()
    {
        return new CreateOrder
        {
            CustomerEmail = "ada@example.com",
            Lines =
            [
                new CreateOrderLine
                {
                    Sku = "SKU-001",
                    Quantity = 1
                }
            ],
            Total = 10
        };
    }

    private static JsonElement FindObjectSchema(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("properties", out var properties) &&
                properties.TryGetProperty(propertyName, out _))
            {
                return element;
            }

            foreach (var property in element.EnumerateObject())
            {
                try
                {
                    return FindObjectSchema(property.Value, propertyName);
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                try
                {
                    return FindObjectSchema(item, propertyName);
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        throw new InvalidOperationException($"Could not find an object schema with property '{propertyName}'.");
    }
}

internal sealed class SampleApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
    where TStartup : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseStartup<TStartup>();
    }
}
