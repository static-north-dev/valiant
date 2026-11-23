using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StaticNorth.Valiant.Validation.AspNetCore.OpenApi;

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

        ApplyModelMetadata(schema, context.JsonTypeInfo);
        return Task.CompletedTask;
    }

    private void ApplyModelMetadata(OpenApiSchema schema, JsonTypeInfo jsonTypeInfo)
    {
#if NET11_0_OR_GREATER
        // ASP.NET Core exports union branches in case order and invokes transformers on the union itself.
        if (jsonTypeInfo.Kind == JsonTypeInfoKind.Union && schema.AnyOf is { Count: > 0 } cases &&
            cases.Count == jsonTypeInfo.UnionCases.Count)
        {
            for (var index = 0; index < cases.Count; index++)
            {
                if (cases[index] is OpenApiSchema caseSchema)
                {
                    var caseType = jsonTypeInfo.UnionCases[index].CaseType;
                    ApplyModelMetadata(caseSchema, jsonTypeInfo.Options.GetTypeInfo(caseType));
                }
            }
        }
#endif

        if (!_propertiesByModelType.TryGetValue(jsonTypeInfo.Type, out var properties) ||
            schema.Properties is null ||
            schema.Properties.Count == 0)
        {
            return;
        }

        var jsonProperties = CreateJsonPropertyMap(jsonTypeInfo);
        var propertyGroups = properties
            .Where(property => property.PropertyName.IndexOfAny(['.', '[']) < 0 && jsonProperties.ContainsKey(property.PropertyName))
            .GroupBy(property => jsonProperties[property.PropertyName], StringComparer.Ordinal);
        foreach (var propertyGroup in propertyGroups)
        {
            if (!schema.Properties.TryGetValue(propertyGroup.Key, out var propertySchema))
            {
                continue;
            }

            var schemaType = propertySchema.Type;
            if (propertySchema is OpenApiSchemaReference reference)
            {
                // Constraints belong to this property occurrence, not the shared component.
                propertySchema = new OpenApiSchema { AllOf = [reference] };
                schema.Properties[propertyGroup.Key] = propertySchema;
            }

            if (propertySchema is not OpenApiSchema openApiPropertySchema)
            {
                continue;
            }

            ApplyMetadata(schema, propertyGroup.Key, openApiPropertySchema, schemaType, propertyGroup);
        }
    }

    private static Dictionary<string, string> CreateJsonPropertyMap(JsonTypeInfo jsonTypeInfo)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in jsonTypeInfo.Properties)
        {
            if (property.AttributeProvider is MemberInfo member)
            {
                map[member.Name] = property.Name;
            }

            map.TryAdd(property.Name, property.Name);
        }

        return map;
    }

    private static void ApplyMetadata(
        OpenApiSchema parentSchema,
        string jsonPropertyName,
        OpenApiSchema propertySchema,
        JsonSchemaType? schemaType,
        IEnumerable<ValiantPropertyValidationMetadata> properties)
    {
        var rules = new JsonArray();
        foreach (var property in properties)
        {
            foreach (var check in property.Checks)
            {
                var isConditional = property.IsConditional || check.IsConditional;
                rules.Add(CreateRuleExtension(check, isConditional));

                if (!isConditional && check.Severity is null or nameof(ValiantSeverity.Error))
                {
                    ApplySchemaConstraint(
                        parentSchema,
                        jsonPropertyName,
                        propertySchema,
                        schemaType ?? GetSizeSchemaType(property.PropertyType),
                        check);
                }
            }
        }

        if (rules.Count > 0)
        {
            propertySchema.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
            propertySchema.Extensions[RulesExtensionName] = new JsonNodeExtension(rules);
        }
    }

    private static JsonObject CreateRuleExtension(ValiantValidationCheckMetadata check, bool isConditional)
    {
        var rule = new JsonObject
        {
            ["kind"] = check.Kind,
            ["severity"] = check.Severity ?? "Error",
            ["conditional"] = isConditional,
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
        JsonSchemaType? schemaType,
        ValiantValidationCheckMetadata check)
    {
        switch (check.Kind)
        {
            case "Required":
                MarkRequired(parentSchema, jsonPropertyName, propertySchema);
                if (schemaType == JsonSchemaType.String || schemaType == (JsonSchemaType.String | JsonSchemaType.Null))
                {
                    propertySchema.MinLength = Max(propertySchema.MinLength, 1);
                }

                break;
            case "NotNull":
                MarkRequired(parentSchema, jsonPropertyName, propertySchema);
                break;
            case "NotEmpty":
                MarkRequired(parentSchema, jsonPropertyName, propertySchema);
                ApplyMinimumSize(propertySchema, schemaType, 1);
                break;
            case "LengthBetween":
                if (TryGetInt(check, 0, out var minLength) && TryGetInt(check, 1, out var maxLength))
                {
                    ApplyMinimumSize(propertySchema, schemaType, minLength);
                    ApplyMaximumSize(propertySchema, schemaType, maxLength);
                }

                break;
            case "MinLength":
                if (TryGetInt(check, 0, out var min))
                {
                    ApplyMinimumSize(propertySchema, schemaType, min);
                }

                break;
            case "MaxLength":
                if (TryGetInt(check, 0, out var max))
                {
                    ApplyMaximumSize(propertySchema, schemaType, max);
                }

                break;
            case "Count":
                if (TryGetInt(check, 0, out var count))
                {
                    ApplyMinimumSize(propertySchema, schemaType, count);
                    ApplyMaximumSize(propertySchema, schemaType, count);
                }

                break;
            case "CountBetween":
                if (TryGetInt(check, 0, out var minCount) && TryGetInt(check, 1, out var maxCount))
                {
                    ApplyMinimumSize(propertySchema, schemaType, minCount);
                    ApplyMaximumSize(propertySchema, schemaType, maxCount);
                }

                break;
            case "MinCount":
                if (TryGetInt(check, 0, out var minimumCount))
                {
                    ApplyMinimumSize(propertySchema, schemaType, minimumCount);
                }

                break;
            case "MaxCount":
                if (TryGetInt(check, 0, out var maximumCount))
                {
                    ApplyMaximumSize(propertySchema, schemaType, maximumCount);
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
        parentSchema.Required ??= new HashSet<string>(StringComparer.Ordinal);
        parentSchema.Required.Add(jsonPropertyName);
        if (propertySchema.Type is { } type && type != JsonSchemaType.Null)
        {
            propertySchema.Type = type & ~JsonSchemaType.Null;
            return;
        }

        if (propertySchema.Not is null)
        {
            propertySchema.Not = new OpenApiSchema { Type = JsonSchemaType.Null };
        }
        else if (propertySchema.Not is not OpenApiSchema { Type: JsonSchemaType.Null })
        {
            propertySchema.AllOf ??= [];
            propertySchema.AllOf.Add(new OpenApiSchema { Not = new OpenApiSchema { Type = JsonSchemaType.Null } });
        }
    }

    private static void ApplyMinimumSize(OpenApiSchema schema, JsonSchemaType? schemaType, int minimum)
    {
        if (minimum < 0)
        {
            return;
        }

        switch (schemaType & ~JsonSchemaType.Null)
        {
            case JsonSchemaType.String:
                schema.MinLength = Max(schema.MinLength, minimum);
                break;
            case JsonSchemaType.Object:
                schema.MinProperties = Max(schema.MinProperties, minimum);
                break;
            case JsonSchemaType.Array:
                schema.MinItems = Max(schema.MinItems, minimum);
                break;
        }
    }

    private static void ApplyMaximumSize(OpenApiSchema schema, JsonSchemaType? schemaType, int maximum)
    {
        if (maximum < 0)
        {
            return;
        }

        switch (schemaType & ~JsonSchemaType.Null)
        {
            case JsonSchemaType.String:
                schema.MaxLength = Min(schema.MaxLength, maximum);
                break;
            case JsonSchemaType.Object:
                schema.MaxProperties = Min(schema.MaxProperties, maximum);
                break;
            case JsonSchemaType.Array:
                schema.MaxItems = Min(schema.MaxItems, maximum);
                break;
        }
    }

    private static JsonSchemaType? GetSizeSchemaType(Type type)
    {
        if (type == typeof(string))
        {
            return JsonSchemaType.String;
        }

        if (typeof(System.Collections.IDictionary).IsAssignableFrom(type) ||
            IsGenericDictionary(type) ||
            type.GetInterfaces().Any(IsGenericDictionary))
        {
            return JsonSchemaType.Object;
        }

        return typeof(System.Collections.IEnumerable).IsAssignableFrom(type) ? JsonSchemaType.Array : null;

        static bool IsGenericDictionary(Type candidate) =>
            candidate.IsGenericType &&
            candidate.GetGenericTypeDefinition() is var definition &&
            (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>));
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
                check.Arguments[index].TrimEnd('m', 'M', 'd', 'D', 'f', 'F', 'u', 'U', 'l', 'L'),
                NumberStyles.Float,
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

    private static bool TryParseDecimal(string? value, out decimal parsed)
    {
        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("G", CultureInfo.InvariantCulture);
    }
}
