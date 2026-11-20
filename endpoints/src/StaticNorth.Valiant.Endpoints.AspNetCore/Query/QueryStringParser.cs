using System.Collections;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Parses query strings into typed objects while enforcing configured query formats.
/// </summary>
public static class QueryStringParser
{
    private static readonly ConditionalWeakTable<Type, QueryType> QueryTypes = new();
    private static readonly QueryParsingOptions DefaultOptions = new();
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
        var value = ParseCore(queryString, typeof(T), options ?? DefaultOptions, out var errors);
        return new QueryParseResult<T>(errors.Count == 0 ? (T?)value : default, errors);
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
        var value = ParseCore(queryString, targetType, options ?? DefaultOptions, out var errors);
        return new QueryParseResult(value, errors);
    }

    private static object? ParseCore(
        string? queryString,
        Type targetType,
        QueryParsingOptions options,
        out IReadOnlyList<QueryError> errors)
    {
        return new QueryInput(queryString, options).Bind(targetType, out errors);
    }

    internal sealed class QueryInput
    {
        private readonly QueryNode _root = new();
        private readonly QueryParsingOptions _options;
        private readonly List<QueryError> _errors = [];

        internal QueryInput(string? queryString, QueryParsingOptions options)
        {
            _options = options;
            ValidateOptions(options, _errors);
            ReadParameters(queryString, options, _root, _errors);
        }

        internal object? Bind(Type targetType, out IReadOnlyList<QueryError> errors)
        {
            object? value = null;
            if (_errors.Count == 0)
            {
                _ = TryBindValue(targetType, _root, string.Empty, _options, _errors, out value);
            }

            errors = _errors.Count == 0 ? Array.Empty<QueryError>() : _errors;
            return _errors.Count == 0 ? value : null;
        }
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

    private static void ReadParameters(string? queryString, QueryParsingOptions options, QueryNode root, List<QueryError> errors)
    {
        var query = queryString.AsSpan();
        if (!query.IsEmpty && query[0] == '?')
        {
            query = query[1..];
        }

        var order = 0;
        while (!query.IsEmpty)
        {
            var end = query.IndexOf('&');
            var part = end < 0 ? query : query[..end];
            query = end < 0 ? [] : query[(end + 1)..];
            if (part.IsEmpty)
            {
                continue;
            }

            var separator = part.IndexOf('=');
            var key = Decode((separator < 0 ? part : part[..separator]).ToString());
            var value = separator < 0 ? string.Empty : part[(separator + 1)..].ToString();
            if (TryCreateParameter(key, value, order++, errors, out var parameter) &&
                TryValidateConfiguredFormat(parameter, options, errors))
            {
                root.Add(parameter);
            }
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
            null,
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

        var shape = QueryTypes.GetValue(targetType, static type => new QueryType(type));
        if (shape.ElementType is { } elementType)
        {
            return TryBindCollection(targetType, elementType, shape.ListType, node, path, options, errors, out value);
        }

        if (shape.IsSimple)
        {
            return TryBindSimple(targetType, node, path, errors, out value);
        }

        return TryBindObject(shape, node, path, options, errors, out value);
    }

    private static bool TryBindSimple(Type targetType, QueryNode node, string path, List<QueryError> errors, out object? value)
    {
        value = null;
        if (node.Values.Count == 0)
        {
            return true;
        }

        if (node.Values.Exists(static queryValue => queryValue.ArrayFormat is not null))
        {
            errors.Add(ValidationError(
                path,
                "InvalidQueryValue",
                $"Query path '{path}' uses array syntax but the target type is scalar."));
            return false;
        }

        var lastValue = node.Values[^1];

        return TryConvertValue(lastValue.Value, targetType, path, allowEmptyNullable: true, errors, out value);
    }

    private static bool TryBindCollection(
        Type targetType,
        Type elementType,
        Type? listType,
        QueryNode node,
        string path,
        QueryParsingOptions options,
        List<QueryError> errors,
        out object? value)
    {
        value = null;
        var values = node.Values;
        if (values is [{ ArrayFormat: null } singleValue] && LooksLikeJsonValue(singleValue.Value))
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

        var scalarCount = 0;
        var needsReordering = false;
        foreach (var item in values)
        {
            scalarCount += item.ArrayFormat is null ? 1 : 0;
            needsReordering |= item.ArrayFormat is not null || item.EncodedValue.Contains(',');
        }

        var allowsRepeated = Supports(options.SupportedArrayFormats, QueryArrayFormat.RepeatedKeys);
        var allowsComma = Supports(options.SupportedArrayFormats, QueryArrayFormat.CommaSeparated);
        if (scalarCount > 1 && !allowsRepeated)
        {
            errors.Add(ConfigurationError(
                path,
                "DisabledQueryArrayFormat",
                $"Query path '{path}' uses repeated array keys, but format '{QueryArrayFormat.RepeatedKeys}' is disabled."));
            return false;
        }

        if (scalarCount == 1 && !allowsRepeated && !allowsComma &&
            values.Exists(static item => item.ArrayFormat is null && !item.EncodedValue.Contains(',')))
        {
            errors.Add(ConfigurationError(
                path,
                "DisabledQueryArrayFormat",
                $"Query path '{path}' uses a scalar array value, but neither '{QueryArrayFormat.RepeatedKeys}' nor '{QueryArrayFormat.CommaSeparated}' is enabled."));
            return false;
        }

        List<string>? orderedItems = null;
        if (needsReordering)
        {
            orderedItems = new List<string>(values.Count);
            List<QueryValue>? indexed = null;
            foreach (var item in values)
            {
                if (item.ArrayFormat == QueryArrayFormat.IndexedBrackets)
                {
                    (indexed ??= []).Add(item);
                }
                else if (item.ArrayFormat is null)
                {
                    if (!item.EncodedValue.Contains(','))
                    {
                        orderedItems.Add(item.Value);
                        continue;
                    }

                    if (!allowsComma)
                    {
                        errors.Add(ConfigurationError(
                            path,
                            "DisabledQueryArrayFormat",
                            $"Query path '{path}' uses comma-separated array values, but format '{QueryArrayFormat.CommaSeparated}' is disabled."));
                        return false;
                    }

                    foreach (var part in item.EncodedValue.Split(','))
                    {
                        orderedItems.Add(Decode(part));
                    }
                }
            }

            foreach (var item in values)
            {
                if (item.ArrayFormat == QueryArrayFormat.Brackets)
                {
                    orderedItems.Add(item.Value);
                }
            }

            if (indexed is not null)
            {
                indexed.Sort(static (left, right) => left.ArrayIndex == right.ArrayIndex
                    ? left.Order.CompareTo(right.Order)
                    : Nullable.Compare(left.ArrayIndex, right.ArrayIndex));
                foreach (var item in indexed)
                {
                    orderedItems.Add(item.Value);
                }
            }
        }

        var count = orderedItems?.Count ?? values.Count;
        var array = targetType.IsArray ? Array.CreateInstance(elementType, count) : null;
        var list = array is null ? (IList)Activator.CreateInstance(listType!)! : null;
        for (var index = 0; index < count; index++)
        {
            var item = orderedItems is null ? values[index].Value : orderedItems[index];
            if (!TryConvertValue(item, elementType, path, allowEmptyNullable: false, errors, out var itemValue, index))
            {
                continue;
            }

            if (array is not null)
            {
                array.SetValue(itemValue, index);
            }
            else
            {
                list!.Add(itemValue);
            }
        }

        if (errors.Count > 0)
        {
            return false;
        }

        value = (object?)array ?? list;
        return true;
    }

    private static bool TryBindObject(
        QueryType shape,
        QueryNode node,
        string path,
        QueryParsingOptions options,
        List<QueryError> errors,
        out object? value)
    {
        value = null;
        var nonNullableType = shape.Type;

        var jsonIndex = node.Values.FindLastIndex(static item => item.ArrayFormat is null && LooksLikeJsonValue(item.Value));
        if (jsonIndex >= 0)
        {
            if (!options.EnableJsonQueryValues)
            {
                errors.Add(ConfigurationError(
                    path,
                    "DisabledJsonQueryValues",
                    $"Query path '{path}' uses a JSON query value, but JSON query values are disabled."));
                return false;
            }

            return TryDeserializeJson(nonNullableType, node.Values[jsonIndex].Value, path, errors, out value);
        }

        if (node.Values.Count > 0 && !node.HasChildren)
        {
            errors.Add(ValidationError(
                path,
                "InvalidQueryValue",
                $"Query path '{path}' cannot bind a scalar query value to complex type '{nonNullableType.Name}'."));
            return false;
        }

        if (!TryCreateObject(shape, path, errors, out var instance))
        {
            return false;
        }

        foreach (var property in shape.Properties)
        {
            if (node.GetChild(property.Name) is not { } child)
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

    private static bool TryCreateObject(QueryType shape, string path, List<QueryError> errors, out object instance)
    {
        instance = null!;

        var type = shape.Type;
        if (type.IsAbstract || type.IsInterface)
        {
            errors.Add(ValidationError(
                path,
                "UnsupportedQueryType",
                $"Query path '{path}' cannot create abstract or interface type '{type.Name}'."));
            return false;
        }

        var constructor = shape.Constructor;
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
        out object? value,
        int? arrayIndex = null)
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

            path = arrayIndex is { } emptyIndex ? CombineArrayPath(path, emptyIndex) : path;
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

        path = arrayIndex is { } invalidIndex ? CombineArrayPath(path, invalidIndex) : path;
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

    private sealed class QueryType
    {
        public QueryType(Type type)
        {
            Type = Nullable.GetUnderlyingType(type) ?? type;
            IsSimple = IsSimpleType(type);
            if (!IsSimple && TryGetCollectionElementType(type, out var elementType))
            {
                ElementType = elementType;
                ListType = type.IsArray ? null : typeof(List<>).MakeGenericType(elementType);
            }
            else if (!IsSimple)
            {
                Constructor = Type.GetConstructor(System.Type.EmptyTypes);
                Properties = GetBindableProperties(Type).ToArray();
            }
        }

        public Type Type { get; }
        public bool IsSimple { get; }
        public Type? ElementType { get; }
        public Type? ListType { get; }
        public ConstructorInfo? Constructor { get; }
        public PropertyInfo[] Properties { get; } = [];
    }

    private sealed class QueryNode
    {
        private Dictionary<string, QueryNode>? _children;
        private List<QueryValue>? _values;
        private static readonly List<QueryValue> EmptyValues = [];

        public List<QueryValue> Values => _values ?? EmptyValues;

        public bool HasChildren => _children is not null;

        public void Add(QueryParameter parameter)
        {
            var current = this;
            if (parameter.Path is null)
            {
                current = current.GetOrAddChild(parameter.OriginalKey);
            }
            else
            {
                foreach (var segment in parameter.Path)
                {
                    current = current.GetOrAddChild(segment);
                }
            }

            (current._values ??= []).Add(new QueryValue(
                parameter.EncodedValue,
                parameter.ArrayFormat,
                parameter.ArrayIndex,
                parameter.Order));
        }

        private QueryNode GetOrAddChild(string name)
        {
            _children ??= new Dictionary<string, QueryNode>(StringComparer.OrdinalIgnoreCase);
            if (!_children.TryGetValue(name, out var child))
            {
                child = new QueryNode();
                _children.Add(name, child);
            }

            return child;
        }

        public QueryNode? GetChild(string name) =>
            _children is not null && _children.TryGetValue(name, out var child) ? child : null;
    }

    private readonly struct QueryValue(
        string encodedValue,
        QueryArrayFormat? arrayFormat,
        int? arrayIndex,
        int order)
    {
        public string EncodedValue { get; } = encodedValue;

        public string Value { get; } = Decode(encodedValue);

        public QueryArrayFormat? ArrayFormat { get; } = arrayFormat;

        public int? ArrayIndex { get; } = arrayIndex;

        public int Order { get; } = order;
    }

    private readonly struct QueryParameter(
        string originalKey,
        string encodedValue,
        string[]? path,
        QueryObjectFormat? objectFormat,
        QueryArrayFormat? arrayFormat,
        int? arrayIndex,
        int order)
    {
        public string OriginalKey { get; } = originalKey;

        public string EncodedValue { get; } = encodedValue;

        public string[]? Path { get; } = path;

        public QueryObjectFormat? ObjectFormat { get; } = objectFormat;

        public QueryArrayFormat? ArrayFormat { get; } = arrayFormat;

        public int? ArrayIndex { get; } = arrayIndex;

        public int Order { get; } = order;
    }
}
