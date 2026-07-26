using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using StaticNorth.Valiant.Validation.AspNetCore.OpenApi;

namespace StaticNorth.Valiant.Validation.Tests;

public sealed class OpenApiIntegrationTests
{
    [Test]
    public async Task Schema_Transformer_Applies_Static_Validation_Metadata_To_OpenApi_Schema()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["userName"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["Age"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" }
            }
        };
        var transformer = new ValiantValidationSchemaTransformer([new OpenApiUserMetadataProvider()]);
        var context = new OpenApiSchemaTransformerContext
        {
            DocumentName = "v1",
            ParameterDescription = null,
            JsonTypeInfo = JsonSerializerOptions.Default.GetTypeInfo(typeof(OpenApiUser)),
            JsonPropertyInfo = null,
            ApplicationServices = new ServiceCollection().BuildServiceProvider()
        };

        await transformer.TransformAsync(schema, context, CancellationToken.None);

        var schemaProperties = schema.Properties ?? throw new InvalidOperationException("OpenAPI schema properties were not initialized.");
        var userNameSchema = (OpenApiSchema)schemaProperties["userName"];
        var ageSchema = (OpenApiSchema)schemaProperties["Age"];

        await Assert.That(schema.Required).Contains("userName");
        await Assert.That(userNameSchema.MinLength).IsEqualTo(1);
        await Assert.That(userNameSchema.MaxLength).IsEqualTo(64);
        await Assert.That(ageSchema.Minimum).IsEqualTo("18");
        await Assert.That(ageSchema.ExclusiveMinimum).IsNull();

        var rules = ((JsonNodeExtension)userNameSchema.Extensions!["x-valiant-rules"]).Node.AsArray();
        await Assert.That(rules).Count().IsEqualTo(3);
        await Assert.That(rules[0]?["message"]?.GetValue<string>()).IsEqualTo("Name is required.");
    }

    [Test]
    public async Task AddValiantOpenApi_Registers_Schema_Transformer()
    {
        var services = new ServiceCollection();

        services.AddValiantOpenApi();

        await Assert.That(services.Any(static descriptor => descriptor.ServiceType == typeof(ValiantValidationSchemaTransformer))).IsTrue();
    }

    private sealed class OpenApiUserMetadataProvider : IValiantValidationMetadataProvider
    {
        public Type ModelType => typeof(OpenApiUser);

        public IReadOnlyList<ValiantPropertyValidationMetadata> Properties { get; } =
        [
            new ValiantPropertyValidationMetadata(
                "Name",
                typeof(string),
                false,
                [
                    new ValiantValidationCheckMetadata(
                        "Required",
                        [],
                        [],
                        "NameRequired",
                        "Name is required.",
                        "Error",
                        false,
                        false),
                    new ValiantValidationCheckMetadata(
                        "MaxLength",
                        ["64"],
                        [],
                        null,
                        null,
                        "Error",
                        false,
                        false),
                    new ValiantValidationCheckMetadata(
                        "MinLength",
                        ["4"],
                        [],
                        null,
                        null,
                        "Error",
                        false,
                        true)
                ]),
            new ValiantPropertyValidationMetadata(
                "Age",
                typeof(int),
                false,
                [
                    new ValiantValidationCheckMetadata(
                        "GreaterThanOrEqualTo",
                        ["18"],
                        [],
                        null,
                        null,
                        "Error",
                        false,
                        false)
                ])
        ];
    }

    private sealed class OpenApiUser
    {
        [JsonPropertyName("userName")]
        public string? Name { get; init; }

        public int Age { get; init; }
    }
}
