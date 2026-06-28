using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Valiant.Validation.AspNetCore.OpenApi;

/// <summary>
/// Applies source-generated Valiant validation metadata to native OpenAPI schemas.
/// </summary>
public sealed class ValiantValidationSchemaTransformer : IOpenApiSchemaTransformer
{
    private const string RulesExtensionName = "x-valiant-rules";
    private readonly IReadOnlyDictionary<Type, IReadOnlyList<ValiantPropertyValidationMetadata>> _propertiesByModelType;

    /// <summary>
    /// Initializes the schema transformer.
    /// </summary>
    /// <param name="metadataProviders">The generated Valiant metadata providers.</param>
    public ValiantValidationSchemaTransformer(IEnumerable<IValiantValidationMetadataProvider> metadataProviders)
    {
        _ = metadataProviders ?? throw new ArgumentNullException(nameof(metadataProviders));
        _propertiesByModelType = metadataProviders
            .GroupBy(static provider => provider.ModelType)
            .ToDictionary(
                static group => group.Key,
                static IReadOnlyList<ValiantPropertyValidationMetadata> (group) => group
                    .SelectMany(static provider => provider.Properties)
                    .ToArray());
    }

    /// <inheritdoc />
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        _ = schema ?? throw new ArgumentNullException(nameof(schema));
        _ = context ?? throw new ArgumentNullException(nameof(context));
        cancellationToken.ThrowIfCancellationRequested();

        if (!_propertiesByModelType.TryGetValue(context.JsonTypeInfo.Type, out var properties) ||
            schema.Properties is null ||
            schema.Properties.Count == 0)
        {
            return Task.CompletedTask;
        }

        var jsonProperties = CreateJsonPropertyMap(context.JsonTypeInfo);
        foreach (var property in properties)
        {
            if (property.PropertyName.IndexOfAny(['.', '[']) >= 0 ||
                !jsonProperties.TryGetValue(property.PropertyName, out var jsonPropertyName) ||
                !schema.Properties.TryGetValue(jsonPropertyName, out var propertySchema) ||
                propertySchema is not OpenApiSchema openApiPropertySchema)
            {
                continue;
            }

            ApplyMetadata(schema, jsonPropertyName, openApiPropertySchema, property);
        }

        return Task.CompletedTask;
    }

    private static Dictionary<string, string> CreateJsonPropertyMap(JsonTypeInfo jsonTypeInfo)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in jsonTypeInfo.Properties)
        {
            if (property.AttributeProvider is PropertyInfo propertyInfo)
            {
                map[propertyInfo.Name] = property.Name;
            }

            map.TryAdd(property.Name, property.Name);
        }

        return map;
    }

    private static void ApplyMetadata(
        OpenApiSchema parentSchema,
        string jsonPropertyName,
        OpenApiSchema propertySchema,
        ValiantPropertyValidationMetadata property)
    {
        var rules = new JsonArray();
        foreach (var check in property.Checks)
        {
            rules.Add(CreateRuleExtension(check));

            if (!property.IsConditional && !check.IsConditional)
            {
                ApplySchemaConstraint(parentSchema, jsonPropertyName, propertySchema, check);
            }
        }

        if (rules.Count > 0)
        {
            propertySchema.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
            propertySchema.Extensions[RulesExtensionName] = new JsonNodeExtension(rules);
        }
    }

    private static JsonObject CreateRuleExtension(ValiantValidationCheckMetadata check)
    {
        var rule = new JsonObject
        {
            ["kind"] = check.Kind,
            ["severity"] = check.Severity ?? "Error",
            ["conditional"] = check.IsConditional,
            ["async"] = check.IsAsync
        };

        if (!string.IsNullOrWhiteSpace(check.Code))
        {
            rule["code"] = check.Code;
        }

        if (!string.IsNullOrWhiteSpace(check.Message))
        {
            rule["message"] = check.Message;
        }

        if (check.Arguments.Count > 0)
        {
            var arguments = new JsonArray();
            foreach (var argument in check.Arguments)
            {
                arguments.Add(argument);
            }

            rule["arguments"] = arguments;
        }

        if (check.TypeArguments.Count > 0)
        {
            var typeArguments = new JsonArray();
            foreach (var typeArgument in check.TypeArguments)
            {
                typeArguments.Add(typeArgument);
            }

            rule["typeArguments"] = typeArguments;
        }

        return rule;
    }

    private static void ApplySchemaConstraint(
        OpenApiSchema parentSchema,
        string jsonPropertyName,
        OpenApiSchema propertySchema,
        ValiantValidationCheckMetadata check)
    {
        switch (check.Kind)
        {
            case "Required":
                MarkRequired(parentSchema, jsonPropertyName, propertySchema);
                if (HasSchemaType(propertySchema, JsonSchemaType.String))
                {
                    propertySchema.MinLength = Max(propertySchema.MinLength, 1);
                }

                break;
            case "NotNull":
                MarkRequired(parentSchema, jsonPropertyName, propertySchema);
                break;
            case "NotEmpty":
                ApplyMinimumSize(propertySchema, 1);
                break;
            case "LengthBetween":
                if (TryGetInt(check, 0, out var minLength) && TryGetInt(check, 1, out var maxLength))
                {
                    propertySchema.MinLength = Max(propertySchema.MinLength, minLength);
                    propertySchema.MaxLength = Min(propertySchema.MaxLength, maxLength);
                }

                break;
            case "MinLength":
                if (TryGetInt(check, 0, out var min))
                {
                    propertySchema.MinLength = Max(propertySchema.MinLength, min);
                }

                break;
            case "MaxLength":
                if (TryGetInt(check, 0, out var max))
                {
                    propertySchema.MaxLength = Min(propertySchema.MaxLength, max);
                }

                break;
            case "Count":
                if (TryGetInt(check, 0, out var count))
                {
                    ApplyExactSize(propertySchema, count);
                }

                break;
            case "CountBetween":
                if (TryGetInt(check, 0, out var minCount) && TryGetInt(check, 1, out var maxCount))
                {
                    ApplyMinimumSize(propertySchema, minCount);
                    ApplyMaximumSize(propertySchema, maxCount);
                }

                break;
            case "MinCount":
                if (TryGetInt(check, 0, out var minimumCount))
                {
                    ApplyMinimumSize(propertySchema, minimumCount);
                }

                break;
            case "MaxCount":
                if (TryGetInt(check, 0, out var maximumCount))
                {
                    ApplyMaximumSize(propertySchema, maximumCount);
                }

                break;
            case "GreaterThan":
                ApplyMinimum(propertySchema, check, exclusive: true);
                break;
            case "GreaterThanOrEqualTo":
                ApplyMinimum(propertySchema, check, exclusive: false);
                break;
            case "LessThan":
                ApplyMaximum(propertySchema, check, exclusive: true);
                break;
            case "LessThanOrEqualTo":
                ApplyMaximum(propertySchema, check, exclusive: false);
                break;
            case "InclusiveBetween":
                ApplyBetween(propertySchema, check, exclusive: false);
                break;
            case "ExclusiveBetween":
                ApplyBetween(propertySchema, check, exclusive: true);
                break;
        }
    }

    private static void MarkRequired(OpenApiSchema parentSchema, string jsonPropertyName, OpenApiSchema propertySchema)
    {
        _ = propertySchema;
        parentSchema.Required ??= new HashSet<string>(StringComparer.Ordinal);
        parentSchema.Required.Add(jsonPropertyName);
    }

    private static void ApplyMinimumSize(OpenApiSchema schema, int minimum)
    {
        if (HasSchemaType(schema, JsonSchemaType.String))
        {
            schema.MinLength = Max(schema.MinLength, minimum);
            return;
        }

        if (HasSchemaType(schema, JsonSchemaType.Object))
        {
            schema.MinProperties = Max(schema.MinProperties, minimum);
            return;
        }

        schema.MinItems = Max(schema.MinItems, minimum);
    }

    private static void ApplyMaximumSize(OpenApiSchema schema, int maximum)
    {
        if (HasSchemaType(schema, JsonSchemaType.String))
        {
            schema.MaxLength = Min(schema.MaxLength, maximum);
            return;
        }

        if (HasSchemaType(schema, JsonSchemaType.Object))
        {
            schema.MaxProperties = Min(schema.MaxProperties, maximum);
            return;
        }

        schema.MaxItems = Min(schema.MaxItems, maximum);
    }

    private static void ApplyExactSize(OpenApiSchema schema, int count)
    {
        ApplyMinimumSize(schema, count);
        ApplyMaximumSize(schema, count);
    }

    private static void ApplyMinimum(OpenApiSchema schema, ValiantValidationCheckMetadata check, bool exclusive)
    {
        if (!TryGetDecimal(check, 0, out var value))
        {
            return;
        }

        if (exclusive)
        {
            schema.ExclusiveMinimum = Max(schema.ExclusiveMinimum, value);
            return;
        }

        schema.Minimum = Max(schema.Minimum, value);
    }

    private static void ApplyMaximum(OpenApiSchema schema, ValiantValidationCheckMetadata check, bool exclusive)
    {
        if (!TryGetDecimal(check, 0, out var value))
        {
            return;
        }

        if (exclusive)
        {
            schema.ExclusiveMaximum = Min(schema.ExclusiveMaximum, value);
            return;
        }

        schema.Maximum = Min(schema.Maximum, value);
    }

    private static void ApplyBetween(OpenApiSchema schema, ValiantValidationCheckMetadata check, bool exclusive)
    {
        if (!TryGetDecimal(check, 0, out var minimum) || !TryGetDecimal(check, 1, out var maximum))
        {
            return;
        }

        if (exclusive)
        {
            schema.ExclusiveMinimum = Max(schema.ExclusiveMinimum, minimum);
            schema.ExclusiveMaximum = Min(schema.ExclusiveMaximum, maximum);
            return;
        }

        schema.Minimum = Max(schema.Minimum, minimum);
        schema.Maximum = Min(schema.Maximum, maximum);
    }

    private static bool TryGetInt(ValiantValidationCheckMetadata check, int index, out int value)
    {
        value = 0;
        return check.Arguments.Count > index &&
            int.TryParse(check.Arguments[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetDecimal(ValiantValidationCheckMetadata check, int index, out decimal value)
    {
        value = 0;
        return check.Arguments.Count > index &&
            decimal.TryParse(
                check.Arguments[index].TrimEnd('m', 'M', 'd', 'D', 'f', 'F'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out value);
    }

    private static int Max(int? current, int value)
    {
        return current.HasValue ? Math.Max(current.Value, value) : value;
    }

    private static int Min(int? current, int value)
    {
        return current.HasValue ? Math.Min(current.Value, value) : value;
    }

    private static string Max(string? current, decimal value)
    {
        return FormatDecimal(TryParseDecimal(current, out var parsed) ? Math.Max(parsed, value) : value);
    }

    private static string Min(string? current, decimal value)
    {
        return FormatDecimal(TryParseDecimal(current, out var parsed) ? Math.Min(parsed, value) : value);
    }

    private static bool HasSchemaType(OpenApiSchema schema, JsonSchemaType type)
    {
        return schema.Type is null || schema.Type.Value.HasFlag(type);
    }

    private static bool TryParseDecimal(string? value, out decimal parsed)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed);
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("G", CultureInfo.InvariantCulture);
    }
}
