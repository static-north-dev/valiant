using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Serializes typed objects into query strings while enforcing configured output formats.
/// </summary>
public static class QueryStringSerializer
{
    private const int MaxObjectDepth = 64;
    private static readonly QuerySerializationOptions DefaultOptions = new();
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serializes a value to a query string.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">The serialization options. Defaults emit repeated arrays and flat top-level objects.</param>
    /// <returns>The serialized query string and any structured errors.</returns>
    public static QuerySerializationResult Serialize(object? value, QuerySerializationOptions? options = null)
    {
        options ??= DefaultOptions;

        var errors = new List<QueryError>();
        if (!TryResolveArrayFormat(options, errors, out var arrayFormat) ||
            !TryResolveObjectFormat(options, errors, out var objectFormat))
        {
            return new QuerySerializationResult(string.Empty, errors);
        }

        var pairs = new List<QueryPair>();
        if (value is not null)
        {
            WriteValue(
                string.Empty,
                value,
                value.GetType(),
                arrayFormat,
                objectFormat,
                options,
                pairs,
                errors,
                depth: 0);
        }

        return errors.Count == 0
            ? new QuerySerializationResult(BuildQueryString(pairs), errors)
            : new QuerySerializationResult(string.Empty, errors);
    }

    private static bool TryResolveArrayFormat(
        QuerySerializationOptions options,
        List<QueryError> errors,
        out QueryArrayFormat format)
    {
        if (!Enum.IsDefined(options.ArrayFormat))
        {
            errors.Add(ConfigurationError(
                string.Empty,
                "InvalidQuerySerializationOptions",
                $"Unsupported array format value '{options.ArrayFormat}'."));
            format = default;
            return false;
        }

        format = options.ArrayFormat;
        return true;
    }

    private static bool TryResolveObjectFormat(
        QuerySerializationOptions options,
        List<QueryError> errors,
        out QueryObjectFormat format)
    {
        if (!Enum.IsDefined(options.ObjectFormat))
        {
            errors.Add(ConfigurationError(
                string.Empty,
                "InvalidQuerySerializationOptions",
                $"Unsupported object format value '{options.ObjectFormat}'."));
            format = default;
            return false;
        }

        format = options.ObjectFormat;
        return true;
    }

    private static void WriteValue(
        string key,
        object value,
        Type declaredType,
        QueryArrayFormat arrayFormat,
        QueryObjectFormat objectFormat,
        QuerySerializationOptions options,
        List<QueryPair> pairs,
        List<QueryError> errors,
        int depth)
    {
        if (depth >= MaxObjectDepth)
        {
            errors.Add(ValidationError(key, "QuerySerializationDepthExceeded", $"Query path '{key}' exceeds the maximum serialization depth."));
            return;
        }

        var valueType = value.GetType();
        if (QueryStringParser.IsSimpleType(valueType))
        {
            if (key.Length == 0)
            {
                errors.Add(ValidationError(
                    key,
                    "InvalidQueryValue",
                    "A scalar root value cannot be serialized without a query key."));
                return;
            }

            pairs.Add(new QueryPair(key, FormatSimpleValue(value), valueIsEncoded: false));
            return;
        }

        if (QueryStringParser.TryGetCollectionElementType(valueType, out var elementType))
        {
            var collectionType = valueType;
            if (declaredType != valueType && QueryStringParser.TryGetCollectionElementType(declaredType, out var declaredElementType))
            {
                collectionType = declaredType;
                elementType = declaredElementType;
            }

            WriteCollection(key, (IEnumerable)value, collectionType, elementType, arrayFormat, options, pairs, errors);
            return;
        }

        WriteObject(key, value, valueType, arrayFormat, objectFormat, options, pairs, errors, depth);
    }

    private static void WriteCollection(
        string key,
        IEnumerable values,
        Type collectionType,
        Type elementType,
        QueryArrayFormat arrayFormat,
        QuerySerializationOptions options,
        List<QueryPair> pairs,
        List<QueryError> errors)
    {
        if (key.Length == 0)
        {
            errors.Add(ValidationError(
                key,
                "InvalidQueryValue",
                "A collection root value cannot be serialized without a query key."));
            return;
        }

        if (!QueryStringParser.CanBindDelimitedCollection(collectionType, elementType))
        {
            if (options.EnableJsonQueryValues)
            {
                WriteJsonValue(key, values, collectionType, pairs, errors);
            }
            else
            {
                errors.Add(ValidationError(
                    key, "UnsupportedQueryType", $"Query path '{key}' requires JSON to represent collection type '{collectionType.Name}'."));
            }

            return;
        }

        var items = values
            .Cast<object?>()
            .Where(static item => item is not null)
            .Select(static item => FormatSimpleValue(item!))
            .ToArray();

        if (items.Length == 0)
        {
            return;
        }

        if (elementType == typeof(string) && options.EnableJsonQueryValues &&
            (arrayFormat == QueryArrayFormat.CommaSeparated ||
                (arrayFormat == QueryArrayFormat.RepeatedKeys && items.Length == 1)) &&
            QueryStringParser.LooksLikeJsonValue(items[0]))
        {
            WriteJsonValue(key, items, typeof(string[]), pairs, errors);
            return;
        }

        switch (arrayFormat)
        {
            case QueryArrayFormat.RepeatedKeys:
                foreach (var item in items)
                {
                    pairs.Add(new QueryPair(key, item, valueIsEncoded: false));
                }

                break;

            case QueryArrayFormat.CommaSeparated:
                pairs.Add(new QueryPair(
                    key,
                    string.Join(",", items.Select(static item => Uri.EscapeDataString(item))),
                    valueIsEncoded: true));
                break;

            case QueryArrayFormat.Brackets:
                foreach (var item in items)
                {
                    pairs.Add(new QueryPair(key + "[]", item, valueIsEncoded: false));
                }

                break;

            case QueryArrayFormat.IndexedBrackets:
                for (var i = 0; i < items.Length; i++)
                {
                    pairs.Add(new QueryPair(
                        key + "[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                        items[i],
                        valueIsEncoded: false));
                }

                break;
        }
    }

    private static void WriteObject(
        string key,
        object value,
        Type valueType,
        QueryArrayFormat arrayFormat,
        QueryObjectFormat objectFormat,
        QuerySerializationOptions options,
        List<QueryPair> pairs,
        List<QueryError> errors,
        int depth)
    {
        if (key.Length > 0 && objectFormat == QueryObjectFormat.Flat)
        {
            if (!options.EnableJsonQueryValues)
            {
                errors.Add(ConfigurationError(
                    key,
                    "DisabledJsonQueryValues",
                    $"Nested query object '{key}' cannot be serialized with flat object formatting unless JSON query values are enabled."));
                return;
            }

            WriteJsonValue(key, value, valueType, pairs, errors);
            return;
        }

        foreach (var property in QueryObjectProperties.Get(valueType))
        {
            if (property.GetMethod is not { IsPublic: true })
            {
                continue;
            }

            var propertyValue = property.GetValue(value);
            if (propertyValue is null)
            {
                continue;
            }

            var propertyKey = key.Length == 0
                ? property.Name
                : GetNestedKey(key, property.Name, objectFormat);
            WriteValue(
                propertyKey,
                propertyValue,
                property.PropertyType,
                arrayFormat,
                objectFormat,
                options,
                pairs,
                errors,
                depth + 1);
        }
    }

    private static void WriteJsonValue(string key, object value, Type type, List<QueryPair> pairs, List<QueryError> errors)
    {
        try
        {
            pairs.Add(new QueryPair(key, JsonSerializer.Serialize(value, type, JsonSerializerOptions), valueIsEncoded: false));
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            errors.Add(ValidationError(key, "InvalidJsonQueryValue", $"Query path '{key}' could not be serialized as JSON."));
        }
    }

    private static string GetNestedKey(string prefix, string propertyName, QueryObjectFormat objectFormat)
    {
        return objectFormat switch
        {
            QueryObjectFormat.DotNotation => prefix + "." + propertyName,
            QueryObjectFormat.DeepObject => prefix + "[" + propertyName + "]",
            _ => propertyName
        };
    }

    private static string FormatSimpleValue(object value)
    {
        return value switch
        {
            bool boolValue => boolValue ? "true" : "false",
            DateTime dateTimeValue => dateTimeValue.ToString("O", CultureInfo.InvariantCulture),
            Guid guidValue => guidValue.ToString("D"),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string BuildQueryString(IReadOnlyList<QueryPair> pairs)
    {
        if (pairs.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append('?');

        for (var i = 0; i < pairs.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('&');
            }

            builder
                .Append(EncodeKey(pairs[i].Key))
                .Append('=')
                .Append(pairs[i].ValueIsEncoded ? pairs[i].Value : Uri.EscapeDataString(pairs[i].Value));
        }

        return builder.ToString();
    }

    private static string EncodeKey(string key)
    {
        return Uri.EscapeDataString(key)
            .Replace("%5B", "[", StringComparison.OrdinalIgnoreCase)
            .Replace("%5D", "]", StringComparison.OrdinalIgnoreCase);
    }

    private static QueryError ValidationError(string path, string code, string message)
    {
        return new QueryError(path, code, message, QueryErrorKind.Validation);
    }

    private static QueryError ConfigurationError(string path, string code, string message)
    {
        return new QueryError(path, code, message, QueryErrorKind.Configuration);
    }

    private readonly struct QueryPair(string key, string value, bool valueIsEncoded)
    {
        public string Key { get; } = key;

        public string Value { get; } = value;

        public bool ValueIsEncoded { get; } = valueIsEncoded;
    }
}
