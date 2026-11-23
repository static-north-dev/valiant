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
    public async Task Schema_Transformer_Preserves_Shape_Conditions_And_Literal_Bounds()
    {
        var sharedReference = new OpenApiSchemaReference("Shared");
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["firstName"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["Items"] = sharedReference,
                ["Dictionary"] = sharedReference,
                ["Ratio"] = new OpenApiSchema { Type = JsonSchemaType.Number, Minimum = "2e-2" },
                ["Optional"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["NegativeBound"] = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };
        using var services = new ServiceCollection().BuildServiceProvider();
        var transformer = new ValiantValidationSchemaTransformer([new SchemaShapeMetadataProvider()]);
        await transformer.TransformAsync(schema, new OpenApiSchemaTransformerContext
        {
            DocumentName = "v1",
            ParameterDescription = null,
            JsonTypeInfo = JsonSerializerOptions.Default.GetTypeInfo<SchemaShape>(),
            JsonPropertyInfo = null,
            ApplicationServices = services
        }, CancellationToken.None);

        var name = (OpenApiSchema)schema.Properties["firstName"];
        var items = (OpenApiSchema)schema.Properties["Items"];
        var dictionary = (OpenApiSchema)schema.Properties["Dictionary"];
        var ratio = (OpenApiSchema)schema.Properties["Ratio"];
        var optional = (OpenApiSchema)schema.Properties["Optional"];
        var negativeBound = (OpenApiSchema)schema.Properties["NegativeBound"];
        await Assert.That(schema.Required).Contains("firstName");
        await Assert.That(name.Type).IsEqualTo(JsonSchemaType.String);
        await Assert.That(name.MinLength).IsEqualTo(1);
        await Assert.That(items.MinItems).IsEqualTo(2);
        await Assert.That(items.MinLength).IsNull();
        await Assert.That(dictionary.MaxProperties).IsEqualTo(3);
        await Assert.That(dictionary.MaxLength).IsNull();
        await Assert.That(ratio.Minimum).IsEqualTo("0.02");
        await Assert.That(ratio.Maximum).IsEqualTo("0.1");
        await Assert.That(optional.MinLength).IsNull();
        await Assert.That(((JsonNodeExtension)optional.Extensions!["x-valiant-rules"]).Node[0]!["conditional"]!.GetValue<bool>()).IsTrue();
        await Assert.That(negativeBound.MinLength).IsNull();
    }

    [Test]
    public async Task Advisory_Rules_Appear_In_Metadata_Without_Restricting_The_Schema()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["userName"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["Age"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                ["Other"] = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };
        var transformer = new ValiantValidationSchemaTransformer([new AdvisoryMetadataProvider()]);
        await transformer.TransformAsync(schema, new OpenApiSchemaTransformerContext
        {
            DocumentName = "v1",
            ParameterDescription = null,
            JsonTypeInfo = JsonSerializerOptions.Default.GetTypeInfo<OpenApiUser>(),
            JsonPropertyInfo = null,
            ApplicationServices = services
        }, CancellationToken.None);

        var name = (OpenApiSchema)schema.Properties["userName"];
        var age = (OpenApiSchema)schema.Properties["Age"];
        await Assert.That(schema.Required?.Contains("userName") ?? false).IsFalse();
        await Assert.That(name.MinLength).IsNull();
        await Assert.That(age.Minimum).IsNull();
        await Assert.That(schema.Required?.Contains("Other") ?? false).IsFalse();
        await Assert.That(((JsonNodeExtension)name.Extensions!["x-valiant-rules"]).Node[0]!["severity"]!.GetValue<string>()).IsEqualTo("Warning");
        await Assert.That(((JsonNodeExtension)age.Extensions!["x-valiant-rules"]).Node[0]!["severity"]!.GetValue<string>()).IsEqualTo("Info");
    }

    [Test]
    public async Task Schema_Transformer_Applies_Static_Validation_Metadata_To_OpenApi_Schema()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["userName"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["Age"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
                ["Other"] = new OpenApiSchema
                {
                    AnyOf = [new OpenApiSchema { Type = JsonSchemaType.String }, new OpenApiSchema { Type = JsonSchemaType.Null }]
                }
            }
        };
        var transformer = new ValiantValidationSchemaTransformer([new OpenApiUserMetadataProvider()]);
        var context = new OpenApiSchemaTransformerContext
        {
            DocumentName = "v1",
            ParameterDescription = null,
            JsonTypeInfo = JsonSerializerOptions.Default.GetTypeInfo<OpenApiUser>(),
            JsonPropertyInfo = null,
            ApplicationServices = new ServiceCollection().BuildServiceProvider()
        };

        await transformer.TransformAsync(schema, context, CancellationToken.None);

        var schemaProperties = schema.Properties ?? throw new InvalidOperationException("OpenAPI schema properties were not initialized.");
        var userNameSchema = (OpenApiSchema)schemaProperties["userName"];
        var ageSchema = (OpenApiSchema)schemaProperties["Age"];
        var otherSchema = (OpenApiSchema)schemaProperties["Other"];

        await Assert.That(schema.Required).Contains("userName");
        await Assert.That(userNameSchema.Type).IsEqualTo(JsonSchemaType.String);
        await Assert.That(userNameSchema.MinLength).IsEqualTo(1);
        await Assert.That(userNameSchema.MaxLength).IsEqualTo(64);
        await Assert.That(ageSchema.Minimum).IsEqualTo("18");
        await Assert.That(ageSchema.ExclusiveMinimum).IsNull();
        await Assert.That(schema.Required).Contains("Other");
        await Assert.That((otherSchema.Not as OpenApiSchema)?.Type).IsEqualTo(JsonSchemaType.Null);

        var rules = ((JsonNodeExtension)userNameSchema.Extensions!["x-valiant-rules"]).Node.AsArray();
        await Assert.That(rules).Count().IsEqualTo(3);
        await Assert.That(rules[0]?["message"]?.GetValue<string>()).IsEqualTo("Name is required.");
    }

    [Test]
    public async Task Referenced_Property_Gets_An_Independent_Non_Null_Constraint()
    {
        var reference = new OpenApiSchemaReference("Shared");
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["Other"] = reference,
                ["Unvalidated"] = reference
            }
        };
        using var services = new ServiceCollection().BuildServiceProvider();
        var transformer = new ValiantValidationSchemaTransformer([new OpenApiUserMetadataProvider()]);
        await transformer.TransformAsync(schema, new OpenApiSchemaTransformerContext
        {
            DocumentName = "v1",
            ParameterDescription = null,
            JsonTypeInfo = JsonSerializerOptions.Default.GetTypeInfo<OpenApiUser>(),
            JsonPropertyInfo = null,
            ApplicationServices = services
        }, CancellationToken.None);

        var propertySchema = (OpenApiSchema)schema.Properties["Other"];
        await Assert.That(schema.Required).Contains("Other");
        await Assert.That((propertySchema.Not as OpenApiSchema)?.Type).IsEqualTo(JsonSchemaType.Null);
        await Assert.That(ReferenceEquals(propertySchema.AllOf!.Single(), reference)).IsTrue();
        await Assert.That(ReferenceEquals(schema.Properties["Unvalidated"], reference)).IsTrue();
    }

    [Test]
    public async Task AddValiantOpenApi_Registers_Schema_Transformer()
    {
        var services = new ServiceCollection();

        services.AddValiantOpenApi();

        await Assert.That(services.Any(static descriptor => descriptor.ServiceType == typeof(ValiantValidationSchemaTransformer))).IsTrue();
    }

    private sealed class AdvisoryMetadataProvider : IValiantValidationMetadataProvider
    {
        public Type ModelType => typeof(OpenApiUser);

        public IReadOnlyList<ValiantPropertyValidationMetadata> Properties { get; } =
        [
            new("Name", typeof(string), false, [new("Required", [], [], null, null, "Warning", false, false)]),
            new("Age", typeof(int), false, [new("GreaterThanOrEqualTo", ["18"], [], null, null, "Info", false, false)]),
            new("Other", typeof(string), false, [new("Required", [], [], null, null, "(GetSeverity())", false, false)])
        ];
    }

    private sealed class SchemaShapeMetadataProvider : IValiantValidationMetadataProvider
    {
        public Type ModelType => typeof(SchemaShape);

        public IReadOnlyList<ValiantPropertyValidationMetadata> Properties { get; } =
        [
            new("FirstName", typeof(string), false, [new("NotEmpty", [], [], null, null, "Error", false, false)]),
            new("Items", typeof(string[]), false, [new("MinCount", ["2"], [], null, null, "Error", false, false)]),
            new("Dictionary", typeof(Dictionary<string, int>), false, [new("MaxCount", ["3"], [], null, null, "Error", false, false)]),
            new("Ratio", typeof(double), false,
            [
                new("GreaterThanOrEqualTo", ["1e-2d"], [], null, null, "Error", false, false),
                new("LessThanOrEqualTo", ["1e-1d"], [], null, null, "Error", false, false)
            ]),
            new("Optional", typeof(string), true, [new("MinLength", ["2"], [], null, null, "Error", false, false)]),
            new("NegativeBound", typeof(string), false, [new("MinLength", ["-1"], [], null, null, "Error", false, false)])
        ];
    }

    private sealed class SchemaShape
    {
        [JsonInclude]
        [JsonPropertyName("firstName")]
        public string? FirstName = null;

        public string[]? Items { get; init; }

        public Dictionary<string, int>? Dictionary { get; init; }

        public double Ratio { get; init; }

        public string? Optional { get; init; }

        public string? NegativeBound { get; init; }
    }

    private sealed class OpenApiUserMetadataProvider : IValiantValidationMetadataProvider
    {
        public Type ModelType => typeof(OpenApiUser);

        public IReadOnlyList<ValiantPropertyValidationMetadata> Properties { get; } =
        [
            new("Other", typeof(string), false, [new("NotNull", [], [], null, null, "Error", false, false)]),
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
                        false)
                ]),
            new ValiantPropertyValidationMetadata(
                "userName",
                typeof(string),
                false,
                [
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

        public string? Other { get; init; }
    }
}
