using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Valiant.Validation.AspNetCore.Sample.Api;
using Valiant.Validation.AspNetCore.Sample.Models;
using Valiant.Validation.AspNetCore.OpenApi;

namespace Valiant.Validation.AspNetCore.Sample.Scenarios;

internal static class AspNetCoreSample
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();
        ValiantAspNetCoreSetup.ConfigureServices(services);

#pragma warning disable ASP0000
        using var serviceProvider = services.BuildServiceProvider();
#pragma warning restore ASP0000
        using var scope = serviceProvider.CreateScope();
        var provider = scope.ServiceProvider;

        Console.WriteLine("Running ASP.NET validation and problem details checks.");
        var validator = provider.GetRequiredService<IValidator<CreateOrder>>();
        var invalidResult = validator.Validate(new CreateOrder
        {
            CustomerEmail = "",
            Lines = [],
            Total = 0
        });

        SampleAssertions.Require(!invalidResult.IsValid, "ASP.NET sample validator accepted an invalid request model.");
        SampleAssertions.Require(
            invalidResult.ToHttpValidationProblemDetails().Errors.ContainsKey("CustomerEmail"),
            "HTTP problem details did not include the customer email error.");
        SampleAssertions.Require(
            invalidResult.ToMvcValidationProblemDetails().Errors.ContainsKey("Lines"),
            "MVC problem details did not include the order lines error.");
        SampleAssertions.Require(
            provider.GetServices<IValiantValidationMetadataProvider>().Any(metadata => metadata.ModelType == typeof(CreateOrder)),
            "Generated metadata provider was not registered.");
        SampleAssertions.Require(
            provider.GetService<ValiantValidationSchemaTransformer>() is not null,
            "OpenAPI schema transformer was not registered.");

        Console.WriteLine("Running OpenAPI schema transformation checks.");
        var orderSchema = CreateOrderSchema();
        var transformer = provider.GetRequiredService<ValiantValidationSchemaTransformer>();
        await transformer.TransformAsync(
            orderSchema,
            new OpenApiSchemaTransformerContext
            {
                DocumentName = "v1",
                ParameterDescription = null,
                JsonTypeInfo = JsonSerializerOptions.Default.GetTypeInfo(typeof(CreateOrder)),
                JsonPropertyInfo = null,
                ApplicationServices = provider
            },
            CancellationToken.None);

        var orderProperties = orderSchema.Properties ?? throw new InvalidOperationException("OpenAPI schema properties were not initialized.");
        var customerEmailSchema = (OpenApiSchema)orderProperties["customerEmail"];
        var linesSchema = (OpenApiSchema)orderProperties["lines"];
        var totalSchema = (OpenApiSchema)orderProperties["total"];

        SampleAssertions.Require(orderSchema.Required?.Contains("customerEmail") == true, "OpenAPI schema did not mark customerEmail as required.");
        SampleAssertions.Require(customerEmailSchema.MinLength == 1, "OpenAPI schema did not reflect Required as minLength.");
        SampleAssertions.Require(customerEmailSchema.MaxLength == 80, "OpenAPI schema did not reflect MaxLength.");
        SampleAssertions.Require(linesSchema.MinItems == 1, "OpenAPI schema did not reflect MinCount.");
        SampleAssertions.Require(totalSchema.Minimum == "0.01", "OpenAPI schema did not reflect GreaterThanOrEqualTo.");
        SampleAssertions.Require(
            customerEmailSchema.Extensions?["x-valiant-rules"] is JsonNodeExtension,
            "OpenAPI schema did not include x-valiant-rules.");

        Console.WriteLine("Valiant ASP.NET Core sample passed.");
    }

    private static OpenApiSchema CreateOrderSchema()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["customerEmail"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["lines"] = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.Object } },
                ["total"] = new OpenApiSchema { Type = JsonSchemaType.Number, Format = "decimal" }
            }
        };
    }
}
