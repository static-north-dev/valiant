using System.Collections;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Parses query strings into typed objects while enforcing configured query formats.
/// </summary>
public static class QueryStringParser
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Parses a query string into the requested type.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="queryString">The query string to parse. A leading question mark is optional.</param>
    /// <param name="options">The parsing options. Defaults accept only repeated arrays and flat objects.</param>
    /// <returns>The typed parse result and any structured errors.</returns>
    public static QueryParseResult<T> Parse<T>(string? queryString, QueryParsingOptions? options = null)
    {
        var result = Parse(queryString, typeof(T), options);
        return result.IsSuccess
            ? new QueryParseResult<T>((T?)result.Value, result.Errors)
            : new QueryParseResult<T>(default, result.Errors);
    }

    /// <summary>
    /// Parses a query string into the requested type.
    /// </summary>
    /// <param name="queryString">The query string to parse. A leading question mark is optional.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="options">The parsing options. Defaults accept only repeated arrays and flat objects.</param>
    /// <returns>The typed parse result and any structured errors.</returns>
    public static QueryParseResult Parse(string? queryString, Type targetType, QueryParsingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        options ??= new QueryParsingOptions();

        var errors = new List<QueryError>();
        ValidateOptions(options, errors);

        var root = new QueryNode();
        foreach (var parameter in ParseParameters(queryString, errors))
        {
            if (!TryValidateConfiguredFormat(parameter, options, errors))
            {
                continue;
            }

            root.Add(parameter);
        }

        if (errors.Count > 0)
        {
            return new QueryParseResult(default, errors);
        }

        if (!TryBindValue(targetType, root, string.Empty, options, errors, out var value))
        {
            return new QueryParseResult(default, errors);
        }

        return errors.Count == 0
            ? new QueryParseResult(value, errors)
            : new QueryParseResult(default, errors);
    }

    private static void ValidateOptions(QueryParsingOptions options, List<QueryError> errors)
    {
        foreach (var format in options.SupportedArrayFormats)
        {
            if (!Enum.IsDefined(format))
            {
                errors.Add(ConfigurationError(
                    string.Empty,
                    "InvalidQueryParsingOptions",
                    $"Unsupported array format value '{format}'."));
            }
        }

        foreach (var format in options.SupportedObjectFormats)
        {
            if (!Enum.IsDefined(format))
            {
                errors.Add(ConfigurationError(
                    string.Empty,
                    "InvalidQueryParsingOptions",
                    $"Unsupported object format value '{format}'."));
            }
        }
    }

    private static IEnumerable<QueryParameter> ParseParameters(string? queryString, List<QueryError> errors)
    {
        if (string.IsNullOrEmpty(queryString))
        {
            yield break;
        }

        var query = queryString[0] == '?' ? queryString[1..] : queryString;
        if (query.Length == 0)
        {
            yield break;
        }

        var order = 0;
        foreach (var part in query.Split('&'))
        {
            if (part.Length == 0)
            {
                continue;
            }

            var separatorIndex = part.IndexOf('=');
            var encodedKey = separatorIndex >= 0 ? part[..separatorIndex] : part;
            var encodedValue = separatorIndex >= 0 ? part[(separatorIndex + 1)..] : string.Empty;
            var key = Decode(encodedKey);
            var value = Decode(encodedValue);

            if (!TryCreateParameter(key, value, order, errors, out var parameter))
            {
                order++;
                continue;
            }

            order++;
            yield return parameter;
        }
    }

    private static string Decode(string value)
    {
        return WebUtility.UrlDecode(value);
    }

    private static bool TryCreateParameter(
        string key,
        string value,
        int order,
        List<QueryError> errors,
        out QueryParameter parameter)
    {
        parameter = default!;

        if (key.Length == 0)
        {
            errors.Add(ValidationError(
                string.Empty,
                "InvalidQueryKey",
                "Query keys cannot be empty."));
            return false;
        }

        if (key.Contains('[') || key.Contains(']'))
        {
            return TryCreateBracketParameter(key, value, order, errors, out parameter);
        }

        if (key.Contains('.'))
        {
            var path = key.Split('.');
            if (path.Any(static segment => segment.Length == 0))
            {
                errors.Add(ValidationError(
                    key,
                    "InvalidQueryKey",
                    $"Query key '{key}' contains an empty dot-notation segment."));
                return false;
            }

            parameter = new QueryParameter(
                key,
                value,
                path,
                QueryObjectFormat.DotNotation,
                null,
                null,
                order);
            return true;
        }

        parameter = new QueryParameter(
            key,
            value,
            [key],
            QueryObjectFormat.Flat,
            null,
            null,
            order);
        return true;
    }

    private static bool TryCreateBracketParameter(
        string key,
        string value,
        int order,
        List<QueryError> errors,
        out QueryParameter parameter)
    {
        parameter = default!;

        var firstBracket = key.IndexOf('[');
        if (firstBracket <= 0)
        {
            errors.Add(ValidationError(
                key,
                "InvalidQueryKey",
                $"Query key '{key}' is not valid bracket notation."));
            return false;
        }

        var root = key[..firstBracket];
        var segments = new List<string>();
        var index = firstBracket;
        while (index < key.Length)
        {
            if (key[index] != '[')
            {
                errors.Add(ValidationError(
                    key,
                    "InvalidQueryKey",
                    $"Query key '{key}' is not valid bracket notation."));
                return false;
            }

            var closeIndex = key.IndexOf(']', index + 1);
            if (closeIndex < 0)
            {
                errors.Add(ValidationError(
                    key,
                    "InvalidQueryKey",
                    $"Query key '{key}' is missing a closing bracket."));
                return false;
            }

            segments.Add(key[(index + 1)..closeIndex]);
            index = closeIndex + 1;
        }

        if (segments.Count == 0)
        {
            errors.Add(ValidationError(
                key,
                "InvalidQueryKey",
                $"Query key '{key}' is not valid bracket notation."));
            return false;
        }

        if (segments.Count == 1)
        {
            var segment = segments[0];
            if (segment.Length == 0)
            {
                parameter = new QueryParameter(
                    key,
                    value,
                    [root],
                    null,
                    QueryArrayFormat.Brackets,
                    null,
                    order);
                return true;
            }

            if (int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var arrayIndex))
            {
                if (arrayIndex < 0)
                {
                    errors.Add(ValidationError(
                        key,
                        "InvalidQueryKey",
                        $"Query key '{key}' uses a negative array index."));
                    return false;
                }

                parameter = new QueryParameter(
                    key,
                    value,
                    [root],
                    null,
                    QueryArrayFormat.IndexedBrackets,
                    arrayIndex,
                    order);
                return true;
            }
        }

        if (segments.Any(static segment => segment.Length == 0 || int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            errors.Add(ValidationError(
                key,
                "InvalidQueryKey",
                $"Query key '{key}' mixes array and object bracket notation, which is not supported."));
            return false;
        }

        var path = new string[segments.Count + 1];
        path[0] = root;
        for (var i = 0; i < segments.Count; i++)
        {
            path[i + 1] = segments[i];
        }

        parameter = new QueryParameter(
            key,
            value,
            path,
            QueryObjectFormat.DeepObject,
            null,
            null,
            order);
        return true;
    }

    private static bool TryValidateConfiguredFormat(
        QueryParameter parameter,
        QueryParsingOptions options,
        List<QueryError> errors)
    {
        if (parameter.ArrayFormat is { } arrayFormat)
        {
            if (!Supports(options.SupportedArrayFormats, arrayFormat))
            {
                errors.Add(ConfigurationError(
                    parameter.OriginalKey,
                    "DisabledQueryArrayFormat",
                    $"Query key '{parameter.OriginalKey}' uses array format '{arrayFormat}', but that format is disabled."));
                return false;
            }

            return true;
        }

        if (parameter.ObjectFormat is { } objectFormat && !Supports(options.SupportedObjectFormats, objectFormat))
        {
            errors.Add(ConfigurationError(
                parameter.OriginalKey,
                "DisabledQueryObjectFormat",
                $"Query key '{parameter.OriginalKey}' uses object format '{objectFormat}', but that format is disabled."));
            return false;
        }

        return true;
    }

    private static bool TryBindValue(
        Type targetType,
        QueryNode node,
        string path,
        QueryParsingOptions options,
        List<QueryError> errors,
        out object? value)
    {
        value = null;

        if (TryGetCollectionElementType(targetType, out var elementType))
        {
            return TryBindCollection(targetType, elementType, node, path, options, errors, out value);
        }

        if (IsSimpleType(targetType))
        {
            return TryBindSimple(targetType, node, path, errors, out value);
        }

        return TryBindObject(targetType, node, path, options, errors, out value);
    }

    private static bool TryBindSimple(Type targetType, QueryNode node, string path, List<QueryError> errors, out object? value)
    {
        value = null;
        if (node.Values.Count == 0)
        {
            return true;
        }

        if (node.Values.Any(static queryValue => queryValue.ArrayFormat is not null))
        {
            errors.Add(ValidationError(
                path,
                "InvalidQueryValue",
                $"Query path '{path}' uses array syntax but the target type is scalar."));
            return false;
        }

        var lastValue = node.Values
            .OrderBy(static queryValue => queryValue.Order)
            .Last();

        return TryConvertValue(lastValue.Value, targetType, path, allowEmptyNullable: true, errors, out value);
    }

    private static bool TryBindCollection(
        Type targetType,
        Type elementType,
        QueryNode node,
        string path,
        QueryParsingOptions options,
        List<QueryError> errors,
        out object? value)
    {
        value = null;

        if (node.Values is [{ ArrayFormat: null } singleValue] && LooksLikeJsonValue(singleValue.Value))
        {
            if (options.EnableJsonQueryValues)
            {
                return TryDeserializeJson(targetType, singleValue.Value, path, errors, out value);
            }
            
            errors.Add(ConfigurationError(
                path,
                "DisabledJsonQueryValues",
                $"Query path '{path}' uses a JSON query value, but JSON query values are disabled."));
            return false;

        }

        var items = new List<(string Value, int Order)>();
        var scalarValues = node.Values
            .Where(static queryValue => queryValue.ArrayFormat is null)
            .OrderBy(static queryValue => queryValue.Order)
            .ToArray();
        var bracketValues = node.Values
            .Where(static queryValue => queryValue.ArrayFormat == QueryArrayFormat.Brackets)
            .OrderBy(static queryValue => queryValue.Order);
        var indexedValues = node.Values
            .Where(static queryValue => queryValue.ArrayFormat == QueryArrayFormat.IndexedBrackets)
            .OrderBy(static queryValue => queryValue.ArrayIndex)
            .ThenBy(static queryValue => queryValue.Order);

        if (scalarValues.Length > 0)
        {
            if (scalarValues.Length > 1 && !Supports(options.SupportedArrayFormats, QueryArrayFormat.RepeatedKeys))
            {
                errors.Add(ConfigurationError(
                    path,
                    "DisabledQueryArrayFormat",
                    $"Query path '{path}' uses repeated array keys, but format '{QueryArrayFormat.RepeatedKeys}' is disabled."));
                return false;
            }

            foreach (var scalarValue in scalarValues)
            {
                if (scalarValue.Value.Contains(',', StringComparison.Ordinal))
                {
                    if (!Supports(options.SupportedArrayFormats, QueryArrayFormat.CommaSeparated))
                    {
                        errors.Add(ConfigurationError(
                            path,
                            "DisabledQueryArrayFormat",
                            $"Query path '{path}' uses comma-separated array values, but format '{QueryArrayFormat.CommaSeparated}' is disabled."));
                        return false;
                    }

                    foreach (var item in scalarValue.Value.Split(','))
                    {
                        items.Add((item, scalarValue.Order));
                    }

                    continue;
                }

                if (scalarValues.Length == 1 &&
                    !Supports(options.SupportedArrayFormats, QueryArrayFormat.RepeatedKeys) &&
                    !Supports(options.SupportedArrayFormats, QueryArrayFormat.CommaSeparated))
                {
                    errors.Add(ConfigurationError(
                        path,
                        "DisabledQueryArrayFormat",
                        $"Query path '{path}' uses a scalar array value, but neither '{QueryArrayFormat.RepeatedKeys}' nor '{QueryArrayFormat.CommaSeparated}' is enabled."));
                    return false;
                }

                items.Add((scalarValue.Value, scalarValue.Order));
            }
        }

        foreach (var bracketValue in bracketValues)
        {
            items.Add((bracketValue.Value, bracketValue.Order));
        }

        foreach (var indexedValue in indexedValues)
        {
            items.Add((indexedValue.Value, indexedValue.Order));
        }

        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        for (var i = 0; i < items.Count; i++)
        {
            var itemPath = CombineArrayPath(path, i);
            if (!TryConvertValue(items[i].Value, elementType, itemPath, allowEmptyNullable: false, errors, out var itemValue))
            {
                continue;
            }

            list.Add(itemValue);
        }

        if (errors.Count > 0)
        {
            return false;
        }

        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            value = array;
            return true;
        }

        value = list;
        return true;
    }

    private static bool TryBindObject(
        Type targetType,
        QueryNode node,
        string path,
        QueryParsingOptions options,
        List<QueryError> errors,
        out object? value)
    {
        value = null;
        var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        var jsonValue = node.Values
            .Where(static queryValue => queryValue.ArrayFormat is null)
            .OrderBy(static queryValue => queryValue.Order)
            .LastOrDefault(static queryValue => LooksLikeJsonValue(queryValue.Value));
        if (jsonValue is not null)
        {
            if (!options.EnableJsonQueryValues)
            {
                errors.Add(ConfigurationError(
                    path,
                    "DisabledJsonQueryValues",
                    $"Query path '{path}' uses a JSON query value, but JSON query values are disabled."));
                return false;
            }

            return TryDeserializeJson(nonNullableType, jsonValue.Value, path, errors, out value);
        }

        if (node.Values.Count > 0 && node.Children.Count == 0)
        {
            errors.Add(ValidationError(
                path,
                "InvalidQueryValue",
                $"Query path '{path}' cannot bind a scalar query value to complex type '{nonNullableType.Name}'."));
            return false;
        }

        if (!TryCreateObject(nonNullableType, path, errors, out var instance))
        {
            return false;
        }

        foreach (var property in GetBindableProperties(nonNullableType))
        {
            if (!node.TryGetChild(property.Name, out var child))
            {
                continue;
            }

            var childPath = CombinePath(path, property.Name);
            if (!TryBindValue(property.PropertyType, child, childPath, options, errors, out var propertyValue))
            {
                continue;
            }

            property.SetValue(instance, propertyValue);
        }

        value = instance;
        return errors.Count == 0;
    }

    private static bool TryDeserializeJson(Type targetType, string json, string path, List<QueryError> errors, out object? value)
    {
        value = null;
        try
        {
            value = JsonSerializer.Deserialize(json, targetType, JsonSerializerOptions);
            return true;
        }
        catch (JsonException exception)
        {
            errors.Add(ValidationError(
                path,
                "InvalidJsonQueryValue",
                $"Query path '{path}' contains invalid JSON: {exception.Message}"));
            return false;
        }
    }

    private static bool TryCreateObject(Type type, string path, List<QueryError> errors, out object instance)
    {
        instance = null!;

        if (type.IsAbstract || type.IsInterface)
        {
            errors.Add(ValidationError(
                path,
                "UnsupportedQueryType",
                $"Query path '{path}' cannot create abstract or interface type '{type.Name}'."));
            return false;
        }

        var constructor = type.GetConstructor(Type.EmptyTypes);
        if (constructor is null)
        {
            errors.Add(ValidationError(
                path,
                "UnsupportedQueryType",
                $"Query path '{path}' cannot create type '{type.Name}' because it does not have a public parameterless constructor."));
            return false;
        }

        instance = constructor.Invoke(null);
        return true;
    }

    private static IEnumerable<PropertyInfo> GetBindableProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetMethod is not null &&
                property.SetMethod is not null &&
                property.GetIndexParameters().Length == 0);
    }

    private static bool TryConvertValue(
        string input,
        Type targetType,
        string path,
        bool allowEmptyNullable,
        List<QueryError> errors,
        out object? value)
    {
        value = null;

        if (targetType == typeof(string))
        {
            value = input;
            return true;
        }

        var nullableType = Nullable.GetUnderlyingType(targetType);
        var nonNullableType = nullableType ?? targetType;
        if (input.Length == 0)
        {
            if (allowEmptyNullable && nullableType is not null)
            {
                value = null;
                return true;
            }

            errors.Add(ValidationError(
                path,
                "InvalidQueryValue",
                $"Query path '{path}' contains an empty value that cannot be converted to '{nonNullableType.Name}'."));
            return false;
        }

        if (nonNullableType == typeof(int) &&
            int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            value = intValue;
            return true;
        }

        if (nonNullableType == typeof(long) &&
            long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            value = longValue;
            return true;
        }

        if (nonNullableType == typeof(Guid) &&
            Guid.TryParse(input, out var guidValue))
        {
            value = guidValue;
            return true;
        }

        if (nonNullableType == typeof(bool) &&
            bool.TryParse(input, out var boolValue))
        {
            value = boolValue;
            return true;
        }

        if (nonNullableType == typeof(decimal) &&
            decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            value = decimalValue;
            return true;
        }

        if (nonNullableType == typeof(DateTime) &&
            DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out var dateTimeValue))
        {
            value = dateTimeValue;
            return true;
        }

        if (nonNullableType.IsEnum)
        {
            try
            {
                value = Enum.Parse(nonNullableType, input, ignoreCase: true);
                return true;
            }
            catch (ArgumentException)
            {
            }
        }

        errors.Add(ValidationError(
            path,
            "InvalidQueryValue",
            $"Query path '{path}' value '{input}' could not be converted to '{nonNullableType.Name}'."));
        return false;
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

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            elementType = type.GetGenericArguments()[0];
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

    private static bool Supports<TFormat>(TFormat[]? formats, TFormat format)
        where TFormat : struct, Enum
    {
        return formats is not null && formats.Contains(format);
    }

    private static bool LooksLikeJsonValue(string value)
    {
        var trimmed = value.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static string CombinePath(string prefix, string segment)
    {
        return prefix.Length == 0 ? segment : prefix + "." + segment;
    }

    private static string CombineArrayPath(string prefix, int index)
    {
        return prefix + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
    }

    private static QueryError ValidationError(string path, string code, string message)
    {
        return new QueryError(path, code, message, QueryErrorKind.Validation);
    }

    private static QueryError ConfigurationError(string path, string code, string message)
    {
        return new QueryError(path, code, message, QueryErrorKind.Configuration);
    }

    private sealed class QueryNode
    {
        private readonly Dictionary<string, QueryNode> _children = new(StringComparer.OrdinalIgnoreCase);

        public List<QueryValue> Values { get; } = [];

        public IReadOnlyDictionary<string, QueryNode> Children => _children;

        public void Add(QueryParameter parameter)
        {
            var current = this;
            foreach (var segment in parameter.Path)
            {
                if (!current._children.TryGetValue(segment, out var child))
                {
                    child = new QueryNode();
                    current._children.Add(segment, child);
                }

                current = child;
            }

            current.Values.Add(new QueryValue(
                parameter.Value,
                parameter.ArrayFormat,
                parameter.ArrayIndex,
                parameter.Order));
        }

        public bool TryGetChild(string name, out QueryNode child)
        {
            return _children.TryGetValue(name, out child!);
        }
    }

    private sealed class QueryValue(
        string value,
        QueryArrayFormat? arrayFormat,
        int? arrayIndex,
        int order)
    {
        public string Value { get; } = value;

        public QueryArrayFormat? ArrayFormat { get; } = arrayFormat;

        public int? ArrayIndex { get; } = arrayIndex;

        public int Order { get; } = order;
    }

    private sealed class QueryParameter(
        string originalKey,
        string value,
        string[] path,
        QueryObjectFormat? objectFormat,
        QueryArrayFormat? arrayFormat,
        int? arrayIndex,
        int order)
    {
        public string OriginalKey { get; } = originalKey;

        public string Value { get; } = value;

        public string[] Path { get; } = path;

        public QueryObjectFormat? ObjectFormat { get; } = objectFormat;

        public QueryArrayFormat? ArrayFormat { get; } = arrayFormat;

        public int? ArrayIndex { get; } = arrayIndex;

        public int Order { get; } = order;
    }
}
