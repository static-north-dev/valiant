using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Parses query strings into typed objects while enforcing configured query formats.
/// </summary>
public static class QueryStringParser
{
    private const int MaxObjectDepth = 64;
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

    internal static bool HasEndpointQueryMembers(Type type, bool useStrictBinding = false)
    {
        var shape = GetQueryType(type);
        return (useStrictBinding ? shape.EndpointRebindProperties : shape.EndpointProperties).Length != 0;
    }

    internal static string[] GetEndpointQueryAliases(Type type) => GetQueryType(type).EndpointAliases;

    internal static bool CanBindDelimitedCollection(Type type, Type elementType)
    {
        var shape = GetQueryType(type);
        return CanBindDelimitedCollection(shape.Type, elementType, shape.ListType);
    }

    private static bool CanBindDelimitedCollection(Type type, Type elementType, Type? listType) =>
        IsSimpleType(elementType) && (type.IsSZArray || listType is not null && type.IsAssignableFrom(listType));

    private static QueryType GetQueryType(Type type) => QueryTypes.GetValue(type, static type => new QueryType(type));

    internal sealed class QueryInput
    {
        private readonly QueryNode _root = new();
        private readonly QueryParsingOptions _options;
        private readonly List<QueryError> _errors = [];

        internal QueryInput(string? queryString, QueryParsingOptions options, string[]? aliases = null)
        {
            _options = options;
            ValidateOptions(options, _errors);
            ReadParameters(queryString, options, _root, _errors, options.UseStrictBinding ? aliases : null);
        }

        internal object? Bind(Type targetType, out IReadOnlyList<QueryError> errors)
        {
            object? value = null;
            if (_errors.Count == 0)
            {
                var shape = GetQueryType(targetType);
                if (_options.UseStrictBinding && (shape.IsSimple || shape.ElementType is not null))
                {
                    _errors.Add(ValidationError(
                        string.Empty,
                        "UnsupportedQueryType",
                        $"Root query type '{shape.Type.Name}' must be an object with named properties."));
                }
                else
                {
                    _ = TryBindValue(targetType, _root, string.Empty, _options, _errors, out value, depth: 0);
                }
            }

            errors = _errors.Count == 0 ? Array.Empty<QueryError>() : _errors;
            return _errors.Count == 0 ? value : null;
        }

        internal object? BindEndpointRequest(Type targetType, object? instance, out IReadOnlyList<QueryError> errors)
        {
            if (!_options.UseStrictBinding)
            {
                var parsed = Bind(targetType, out errors);
                if (parsed is null || instance is null)
                {
                    return parsed;
                }

                foreach (var (property, _, _) in GetQueryType(targetType).EndpointProperties)
                {
                    property.SetValue(instance, property.GetValue(parsed));
                }

                return instance;
            }

            if (_errors.Count == 0)
            {
                var shape = GetQueryType(targetType);
                var rebind = instance is not null;
                if (instance is not null || TryCreateObject(shape, string.Empty, _errors, out instance))
                {
                    BindProperties(
                        instance, rebind ? shape.EndpointRebindProperties : shape.EndpointProperties,
                        _root, string.Empty, _options, _errors, depth: 0, rebind);
                }
            }

            errors = _errors.Count == 0 ? Array.Empty<QueryError>() : _errors;
            return _errors.Count == 0 ? instance : null;
        }
    }

    private static void ValidateOptions(QueryParsingOptions options, List<QueryError> errors)
    {
        foreach (var format in options.SupportedArrayFormats ?? [])
        {
            if (!Enum.IsDefined(format))
            {
                errors.Add(ConfigurationError(
                    string.Empty,
                    "InvalidQueryParsingOptions",
                    $"Unsupported array format value '{format}'."));
            }
        }

        foreach (var format in options.SupportedObjectFormats ?? [])
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

    private static void ReadParameters(
        string? queryString, QueryParsingOptions options, QueryNode root, List<QueryError> errors, string[]? aliases)
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
            if (TryCreateParameter(key, value, order++, errors, aliases, options.UseStrictBinding, out var parameter) &&
                TryValidateConfiguredFormat(parameter, options, errors))
            {
                root.Add(parameter);
            }
        }
    }

    private static string Decode(string value)
    {
        return value.AsSpan().IndexOfAny('%', '+') < 0 ? value : WebUtility.UrlDecode(value);
    }

    private static bool TryCreateParameter(
        string key,
        string value,
        int order,
        List<QueryError> errors,
        string[]? aliases,
        bool useStrictBinding,
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

        var aliasLength = GetRootAliasLength(key, aliases);
        var suffix = key.AsSpan(aliasLength);
        if (suffix.Contains('[') || suffix.Contains(']'))
        {
            var firstBracket = suffix.IndexOf('[');
            return TryCreateBracketParameter(key, value, order, errors, aliasLength, firstBracket < 0 ? -1 : aliasLength + firstBracket, useStrictBinding, out parameter);
        }

        if (suffix.Contains('.'))
        {
            string[] path = aliasLength == 0
                ? key.Split('.')
                : [key[..aliasLength], .. key[(aliasLength + 1)..].Split('.')];
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

    private static int GetRootAliasLength(string key, string[]? aliases)
    {
        foreach (var alias in aliases ?? [])
        {
            if (key.StartsWith(alias, StringComparison.OrdinalIgnoreCase) &&
                (key.Length == alias.Length || key[alias.Length] is '.' or '['))
            {
                return alias.Length;
            }
        }

        return 0;
    }

    private static bool TryCreateBracketParameter(
        string key,
        string value,
        int order,
        List<QueryError> errors,
        int aliasLength,
        int firstBracket,
        bool useStrictBinding,
        out QueryParameter parameter)
    {
        parameter = default!;
        if (firstBracket <= 0)
        {
            errors.Add(ValidationError(key, "InvalidQueryKey", $"Query key '{key}' is not valid bracket notation."));
            return false;
        }

        var root = key[..firstBracket];
        var segments = new List<string>();
        for (var index = firstBracket; index < key.Length;)
        {
            var closeIndex = key.IndexOf(']', index + 1);
            if (key[index] != '[' || closeIndex < 0)
            {
                errors.Add(ValidationError(key, "InvalidQueryKey", $"Query key '{key}' is not valid bracket notation."));
                return false;
            }

            segments.Add(key[(index + 1)..closeIndex]);
            index = closeIndex + 1;
        }

        QueryArrayFormat? arrayFormat = null;
        int? arrayIndex = null;
        var lastSegment = segments[^1];
        if (lastSegment.Length == 0)
        {
            arrayFormat = QueryArrayFormat.Brackets;
            segments.RemoveAt(segments.Count - 1);
        }
        else if (useStrictBinding ? IsArrayIndexSegment(lastSegment) :
            int.TryParse(lastSegment, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            if (!int.TryParse(lastSegment, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                errors.Add(ValidationError(key, "InvalidQueryKey", $"Query key '{key}' uses an invalid array index."));
                return false;
            }

            arrayFormat = QueryArrayFormat.IndexedBrackets;
            arrayIndex = index;
            segments.RemoveAt(segments.Count - 1);
        }

        var hasDotPath = useStrictBinding && root.AsSpan(aliasLength).Contains('.');
        var incompatibleSegments = useStrictBinding
            ? segments.Any(static segment => segment.Length == 0 || IsArrayIndexSegment(segment)) || hasDotPath && segments.Count > 0
            : arrayFormat is not null && segments.Count > 0 || segments.Any(static segment =>
                segment.Length == 0 || int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out _));
        if (incompatibleSegments)
        {
            errors.Add(ValidationError(key, "InvalidQueryKey", $"Query key '{key}' mixes incompatible path segments."));
            return false;
        }

        string[] path;
        QueryObjectFormat? objectFormat = null;
        if (hasDotPath)
        {
            path = aliasLength == 0 ? root.Split('.') : [root[..aliasLength], .. root[(aliasLength + 1)..].Split('.')];
            if (path.Any(static segment => segment.Length == 0))
            {
                errors.Add(ValidationError(key, "InvalidQueryKey", $"Query key '{key}' contains an empty dot-notation segment."));
                return false;
            }

            objectFormat = QueryObjectFormat.DotNotation;
        }
        else
        {
            path = [root, .. segments];
            if (segments.Count > 0)
            {
                objectFormat = QueryObjectFormat.DeepObject;
            }
        }

        parameter = new QueryParameter(key, value, path, objectFormat, arrayFormat, arrayIndex, order);
        return true;
    }

    private static bool IsArrayIndexSegment(string segment)
    {
        var digits = segment.AsSpan(segment.StartsWith('-') ? 1 : 0);
        return !digits.IsEmpty && digits.IndexOfAnyExceptInRange('0', '9') < 0;
    }

    private static bool TryValidateConfiguredFormat(
        QueryParameter parameter,
        QueryParsingOptions options,
        List<QueryError> errors)
    {
        if (parameter.ArrayFormat is { } arrayFormat && !Supports(options.SupportedArrayFormats, arrayFormat))
        {
            errors.Add(ConfigurationError(
                parameter.OriginalKey,
                "DisabledQueryArrayFormat",
                $"Query key '{parameter.OriginalKey}' uses array format '{arrayFormat}', but that format is disabled."));
            return false;
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
        out object? value,
        int depth)
    {
        value = null;

        var shape = GetQueryType(targetType);
        if (shape.ElementType is { } elementType)
        {
            return TryBindCollection(shape.Type, elementType, shape.ListType, node, path, options, errors, out value);
        }

        if (shape.IsSimple)
        {
            return TryBindSimple(targetType, node, path, options.UseStrictBinding, errors, out value);
        }

        return TryBindObject(shape, node, path, options, errors, out value, depth);
    }

    private static bool TryBindSimple(Type targetType, QueryNode node, string path, bool useStrictBinding, List<QueryError> errors, out object? value)
    {
        value = null;
        if (useStrictBinding && node.HasChildren)
        {
            errors.Add(ValidationError(
                path,
                "InvalidQueryValue",
                $"Query path '{path}' uses object syntax but the target type is scalar."));
            return false;
        }

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
        if (options.UseStrictBinding && node.HasChildren)
        {
            errors.Add(ValidationError(
                path,
                "InvalidQueryValue",
                $"Query path '{path}' uses object syntax but the target type is a collection."));
            return false;
        }

        var values = node.Values;
        if (values is [{ ArrayFormat: null } singleValue] &&
            (!options.UseStrictBinding || options.EnableJsonQueryValues || elementType != typeof(string)) &&
            LooksLikeJsonValue(singleValue.Value))
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

        if (!CanBindDelimitedCollection(targetType, elementType, listType))
        {
            errors.Add(ValidationError(
                path,
                "UnsupportedQueryType",
                $"Query path '{path}' cannot bind repeated or delimited values to collection type '{targetType.Name}'."));
            return false;
        }

        var scalarCount = 0;
        var needsReordering = false;
        var useStrictBinding = options.UseStrictBinding;
        foreach (var item in values)
        {
            scalarCount += item.ArrayFormat is null ? 1 : 0;
            needsReordering |= item.ArrayFormat is not null || (useStrictBinding ? item.EncodedValue : item.Value).Contains(',');
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
            values.Exists(item => item.ArrayFormat is null && !(useStrictBinding ? item.EncodedValue : item.Value).Contains(',')))
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
                    var delimitedValue = useStrictBinding ? item.EncodedValue : item.Value;
                    if (!delimitedValue.Contains(','))
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

                    foreach (var part in delimitedValue.Split(','))
                    {
                        orderedItems.Add(useStrictBinding ? Decode(part) : part);
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
        out object? value,
        int depth)
    {
        value = null;
        var nonNullableType = shape.Type;

        if (depth >= MaxObjectDepth)
        {
            errors.Add(ValidationError(path, "QueryParsingDepthExceeded", $"Query path '{path}' exceeds the maximum parsing depth."));
            return false;
        }

        if (options.UseStrictBinding && node.Values.Count > 0 && node.HasChildren)
        {
            errors.Add(ValidationError(
                path,
                "InvalidQueryValue",
                $"Query path '{path}' mixes direct values with nested keys."));
            return false;
        }

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

        BindProperties(instance, shape.Properties, node, path, options, errors, depth);
        value = instance;
        return errors.Count == 0;
    }

    private static void BindProperties(
        object instance,
        QueryProperty[] properties,
        QueryNode node,
        string path,
        QueryParsingOptions options,
        List<QueryError> errors,
        int depth,
        bool rebind = false)
    {
        List<(PropertyInfo Property, object? Value)>? assignments = null;
        foreach (var (property, name, isRequired) in properties)
        {
            if (node.GetChild(name) is not { } child)
            {
                if (options.UseStrictBinding && isRequired && !rebind)
                {
                    var requiredPath = CombinePath(path, name);
                    errors.Add(ValidationError(requiredPath, "MissingQueryValue", $"Query path '{requiredPath}' is required."));
                }

                continue;
            }

            var childPath = CombinePath(path, name);
            if (!TryBindValue(property.PropertyType, child, childPath, options, errors, out var propertyValue, depth + 1))
            {
                continue;
            }

            if (rebind)
            {
                (assignments ??= []).Add((property, propertyValue));
            }
            else
            {
                property.SetValue(instance, propertyValue);
            }
        }

        if (assignments is not null && errors.Count == 0)
        {
            foreach (var (property, value) in assignments)
            {
                property.SetValue(instance, value);
            }
        }
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
        catch (NotSupportedException)
        {
            errors.Add(ValidationError(
                path,
                "UnsupportedQueryType",
                $"Query path '{path}' cannot deserialize JSON to '{targetType.Name}'."));
            return false;
        }
    }

    private static bool TryCreateObject(QueryType shape, string path, List<QueryError> errors, out object instance)
    {
        instance = null!;

        var type = shape.Type;
        if (type.IsByRefLike || type.ContainsGenericParameters || type == typeof(void))
        {
            errors.Add(ValidationError(
                path,
                "UnsupportedQueryType",
                $"Query path '{path}' cannot create type '{type.Name}'."));
            return false;
        }

        if (type.IsAbstract || type.IsInterface)
        {
            errors.Add(ValidationError(
                path,
                "UnsupportedQueryType",
                $"Query path '{path}' cannot create abstract or interface type '{type.Name}'."));
            return false;
        }

        var constructor = shape.Constructor;
        if (constructor is null && type.IsValueType)
        {
            instance = Activator.CreateInstance(type)!;
            return true;
        }

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
        return QueryObjectProperties.Get(type)
            .Where(static property => property.GetMethod is not null &&
                property.SetMethod is { IsPublic: true });
    }

    private static bool CanRebindProperty(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (TryGetCollectionElementType(type, out var elementType))
        {
            type = Nullable.GetUnderlyingType(elementType) ?? elementType;
        }

        if (IsSimpleType(type))
        {
            return true;
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        {
            if (method.Name == "TryParse" && method.ReturnType == typeof(bool) &&
                method.GetParameters() is { Length: 2 or 3 } parameters &&
                parameters[0].ParameterType == typeof(string) &&
                (parameters.Length == 2 || parameters[1].ParameterType == typeof(IFormatProvider)) &&
                parameters[^1].IsOut && parameters[^1].ParameterType == type.MakeByRefType())
            {
                return method.GetCustomAttribute<System.CodeDom.Compiler.GeneratedCodeAttribute>()?.Tool ==
                    "StaticNorth.Valiant.Endpoints.AspNetCore.SourceGeneration";
            }
        }

        return !type.GetInterfaces().Any(static candidate => candidate.IsGenericType &&
            candidate.GetGenericTypeDefinition() == typeof(IParsable<>));
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

        if (nonNullableType.IsEnum && Enum.TryParse(nonNullableType, input, ignoreCase: true, out var enumValue))
        {
            value = enumValue;
            return true;
        }

        path = arrayIndex is { } invalidIndex ? CombineArrayPath(path, invalidIndex) : path;
        errors.Add(ValidationError(
            path,
            "InvalidQueryValue",
            $"Query path '{path}' value '{input}' could not be converted to '{nonNullableType.Name}'."));
        return false;
    }

    internal static bool TryGetCollectionElementType(Type type, out Type elementType)
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

    internal static bool IsSimpleType(Type type)
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

    internal static bool LooksLikeJsonValue(string value)
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
            if (!IsSimple && TryGetCollectionElementType(Type, out var elementType))
            {
                ElementType = elementType;
                ListType = Type.IsArray ? null : typeof(List<>).MakeGenericType(elementType);
            }
            else if (!IsSimple)
            {
                Constructor = Type.GetConstructor(System.Type.EmptyTypes);
                var constructorSetsRequiredMembers = Constructor?.IsDefined(typeof(SetsRequiredMembersAttribute), inherit: false) == true;
                Properties = GetBindableProperties(Type)
                    .Select(property => new QueryProperty(
                        property,
                        property.Name,
                        !constructorSetsRequiredMembers && property.IsDefined(typeof(RequiredMemberAttribute), inherit: false)))
                    .ToArray();
                EndpointProperties = Properties
                    .Select(static property => (Binding: property, Attribute: property.Property.GetCustomAttribute<FromQueryAttribute>()))
                    .Where(static property => property.Attribute is not null)
                    .Select(static property => property.Binding with { Name = property.Attribute!.Name ?? property.Binding.Name })
                    .ToArray();
                EndpointRebindProperties = EndpointProperties
                    .Where(static property => CanRebindProperty(property.Property.PropertyType))
                    .ToArray();
                EndpointAliases = EndpointProperties.Select(static property => property.Name)
                    .Where(static name => name.Contains('.') || name.Contains('[') || name.Contains(']'))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(static name => name.Length)
                    .ToArray();
            }
        }

        public Type Type { get; }
        public bool IsSimple { get; }
        public Type? ElementType { get; }
        public Type? ListType { get; }
        public ConstructorInfo? Constructor { get; }
        public QueryProperty[] Properties { get; } = [];
        public QueryProperty[] EndpointProperties { get; } = [];
        public QueryProperty[] EndpointRebindProperties { get; } = [];
        public string[] EndpointAliases { get; } = [];
    }

    private readonly record struct QueryProperty(PropertyInfo Property, string Name, bool IsRequired = false);

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
