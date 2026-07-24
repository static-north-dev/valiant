using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Serializes typed objects into query strings while enforcing configured output formats.
/// </summary>
public static class QueryStringSerializer
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serializes a value to a query string.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">The serialization options. Defaults emit repeated arrays and flat top-level objects.</param>
    /// <returns>The serialized query string and any structured errors.</returns>
    public static QuerySerializationResult Serialize(object? value, QuerySerializationOptions? options = null)
    {
        options ??= new QuerySerializationOptions();

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
                errors);
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
        Type valueType,
        QueryArrayFormat arrayFormat,
        QueryObjectFormat objectFormat,
        QuerySerializationOptions options,
        List<QueryPair> pairs,
        List<QueryError> errors)
    {
        if (IsSimpleType(valueType))
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

        if (value is not string && TryGetCollectionElementType(valueType, out _))
        {
            WriteCollection(key, (IEnumerable)value, arrayFormat, pairs, errors);
            return;
        }

        WriteObject(key, value, valueType, arrayFormat, objectFormat, options, pairs, errors);
    }

    private static void WriteCollection(
        string key,
        IEnumerable values,
        QueryArrayFormat arrayFormat,
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

        var items = values
            .Cast<object?>()
            .Where(static item => item is not null)
            .Select(static item => FormatSimpleValue(item!))
            .ToArray();

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
        List<QueryError> errors)
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

            pairs.Add(new QueryPair(key, JsonSerializer.Serialize(value, valueType, JsonSerializerOptions), valueIsEncoded: false));
            return;
        }

        foreach (var property in GetSerializableProperties(valueType))
        {
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
                errors);
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

    private static IEnumerable<PropertyInfo> GetSerializableProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetMethod is not null &&
                property.GetIndexParameters().Length == 0);
    }

    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        elementType = typeof(object);

        if (type == typeof(string))
        {
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        foreach (var @interface in type.GetInterfaces().Concat([type]))
        {
            if (@interface.IsGenericType &&
                @interface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                elementType = @interface.GetGenericArguments()[0];
                return true;
            }
        }

        return false;
    }

    private static bool IsSimpleType(Type type)
    {
        var nonNullableType = Nullable.GetUnderlyingType(type) ?? type;
        return nonNullableType == typeof(string) ||
            nonNullableType == typeof(int) ||
            nonNullableType == typeof(long) ||
            nonNullableType == typeof(Guid) ||
            nonNullableType == typeof(bool) ||
            nonNullableType == typeof(decimal) ||
            nonNullableType == typeof(DateTime) ||
            nonNullableType.IsEnum;
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

    private sealed class QueryPair(string key, string value, bool valueIsEncoded)
    {
        public string Key { get; } = key;

        public string Value { get; } = value;

        public bool ValueIsEncoded { get; } = valueIsEncoded;
    }
}
