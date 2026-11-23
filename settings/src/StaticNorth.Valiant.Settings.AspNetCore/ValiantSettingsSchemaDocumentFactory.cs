using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;

namespace StaticNorth.Valiant.Settings.AspNetCore;

/// <summary>
/// Creates UI-friendly schema documents from generated Valiant settings metadata.
/// </summary>
public static class ValiantSettingsSchemaDocumentFactory
{
    /// <summary>
    /// Creates a settings schema document from generated schema providers.
    /// </summary>
    /// <param name="providers">The generated settings schema providers.</param>
    /// <returns>A UI-friendly settings schema document.</returns>
    public static ValiantSettingsSchemaDocument Create(IEnumerable<IValiantSettingsSchemaProvider> providers)
    {
        return Create(providers, serviceProvider: null, includeRuntimeValues: false);
    }

    /// <summary>
    /// Creates a settings schema document from generated schema providers.
    /// </summary>
    /// <param name="providers">The generated settings schema providers.</param>
    /// <param name="serviceProvider">The service provider used to resolve runtime options values.</param>
    /// <param name="includeRuntimeValues">Whether runtime options values should be included.</param>
    /// <returns>A UI-friendly settings schema document.</returns>
    public static ValiantSettingsSchemaDocument Create(
        IEnumerable<IValiantSettingsSchemaProvider> providers,
        IServiceProvider? serviceProvider,
        bool includeRuntimeValues)
    {
        return Create(providers, serviceProvider, includeRuntimeValues, revealSecrets: false, canRevealSecrets: false);
    }

    /// <summary>
    /// Creates a settings schema document from generated schema providers.
    /// </summary>
    /// <param name="providers">The generated settings schema providers.</param>
    /// <param name="serviceProvider">The service provider used to resolve runtime options values.</param>
    /// <param name="includeRuntimeValues">Whether runtime options values should be included.</param>
    /// <param name="revealSecrets">Whether sensitive runtime options values should be unmasked.</param>
    /// <param name="canRevealSecrets">Whether consumers are allowed to request unmasked sensitive values.</param>
    /// <returns>A UI-friendly settings schema document.</returns>
    public static ValiantSettingsSchemaDocument Create(
        IEnumerable<IValiantSettingsSchemaProvider> providers,
        IServiceProvider? serviceProvider,
        bool includeRuntimeValues,
        bool revealSecrets,
        bool canRevealSecrets)
    {
        _ = providers ?? throw new ArgumentNullException(nameof(providers));
        canRevealSecrets = includeRuntimeValues && serviceProvider is not null && canRevealSecrets;
        revealSecrets = canRevealSecrets && revealSecrets;

        var schemas = providers
            .Select(provider => CreateSchema(provider.Schema, serviceProvider, includeRuntimeValues, revealSecrets))
            .OrderBy(static schema => schema.SectionPath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static schema => schema.Name, StringComparer.Ordinal)
            .ToArray();

        return new ValiantSettingsSchemaDocument(schemas, canRevealSecrets,
            revealSecrets && schemas.Any(static schema => schema.RuntimeValues is not null));
    }

    private static ValiantSettingsSchemaEntry CreateSchema(
        ValiantSettingsSchema schema,
        IServiceProvider? serviceProvider,
        bool includeRuntimeValues,
        bool revealSecrets)
    {
        string? runtimeValuesError = null;
        var runtimeValues = includeRuntimeValues && serviceProvider is not null
            ? ValiantSettingsRuntimeValueFactory.TryCreate(schema, serviceProvider, revealSecrets, out runtimeValuesError)
            : null;

        return new ValiantSettingsSchemaEntry(
            schema.Name,
            schema.SettingsType.FullName ?? schema.SettingsType.Name,
            schema.SectionPath,
            [.. schema.Properties.Select(static property => CreateProperty(property, includeDefaults: true))],
            runtimeValues,
            runtimeValuesError)
        {
            AssemblyName = schema.SettingsType.Assembly.FullName
        };
    }

    private static ValiantSettingsSchemaPropertyEntry CreateProperty(ValiantSettingsPropertySchema property, bool includeDefaults)
    {
        includeDefaults = includeDefaults && !ValiantSettingsRuntimeValueFactory.ShouldMask(property);
        return new ValiantSettingsSchemaPropertyEntry(
            property.Name,
            property.ConfigurationKey,
            property.TypeName,
            property.Kind.ToString(),
            property.IsRequired,
            property.IsNullable,
            property.IsSecret,
            includeDefaults ? property.DefaultValue : null,
            property.Item is null ? null : CreateProperty(property.Item, includeDefaults),
            [.. property.Properties.Select(child => CreateProperty(child, includeDefaults))]);
    }
}

internal static class ValiantSettingsRuntimeValueFactory
{
    private static readonly ConditionalWeakTable<Type, Dictionary<string, PropertyInfo>> ReadableProperties = new();
    private const string Mask = "********";

    public static object? TryCreate(
        ValiantSettingsSchema schema,
        IServiceProvider serviceProvider,
        bool revealSecrets,
        out string? error)
    {
        error = null;

        try
        {
            var optionsType = typeof(IOptions<>).MakeGenericType(schema.SettingsType);
            var options = serviceProvider.GetService(optionsType);
            if (options is null)
            {
                error = "No options service is registered for this settings model.";
                return null;
            }

            var settingsType = schema.SettingsType;
            if (options.GetType().Assembly == typeof(IOptions<>).Assembly &&
                !typeof(OptionsWrapper<>).MakeGenericType(settingsType).IsInstanceOfType(options) &&
                !HasOptionsConfiguration(serviceProvider, typeof(IConfigureOptions<>), settingsType) &&
                !HasOptionsConfiguration(serviceProvider, typeof(IPostConfigureOptions<>), settingsType))
            {
                error = "The host has no Options configuration registered for this settings model.";
                return null;
            }

            var value = optionsType.GetProperty(nameof(IOptions<object>.Value))?.GetValue(options);
            if (value is null)
            {
                error = "The options service returned no settings instance.";
                return null;
            }

            return CreateObject(value, schema.SettingsType, schema.Properties, revealSecrets);
        }
        catch (Exception)
        {
            // Options factories, validators and getters can include credentials in exception messages.
            error = "Runtime values could not be loaded. Check the application's options configuration and validation.";
            return null;
        }
    }

    private static bool HasOptionsConfiguration(IServiceProvider services, Type registrationType, Type settingsType)
    {
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(registrationType.MakeGenericType(settingsType));
        return services.GetService(enumerableType) is IEnumerable registrations &&
            registrations.Cast<object>().Any();
    }

    private static IReadOnlyDictionary<string, object?> CreateObject(
        object instance,
        Type type,
        IReadOnlyList<ValiantSettingsPropertySchema> properties,
        bool revealSecrets)
    {
        var values = new Dictionary<string, object?>(properties.Count, StringComparer.Ordinal);
        var readableProperties = ReadableProperties.GetValue(type, static type => GetReadableProperties(type));
        var maskEntryValue = !revealSecrets &&
            type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>) &&
            readableProperties.TryGetValue(nameof(KeyValuePair<string, object>.Key), out var keyProperty) &&
            keyProperty.GetValue(instance) is string key &&
            SensitiveSettingsNames.IsSensitive(key, key);

        foreach (var property in properties)
        {
            if (!revealSecrets && (ShouldMask(property) ||
                (maskEntryValue && property.Name == nameof(KeyValuePair<string, object>.Value))))
            {
                values[property.ConfigurationKey] = Mask;
                continue;
            }

            if (!readableProperties.TryGetValue(property.Name, out var propertyInfo))
            {
                continue;
            }

            var value = propertyInfo.GetValue(instance);
            values[property.ConfigurationKey] = CreateValue(value, propertyInfo.PropertyType, property, revealSecrets);
        }

        return values;
    }

    private static Dictionary<string, PropertyInfo> GetReadableProperties(Type type)
    {
        var properties = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0)
                {
                    properties.TryAdd(property.Name, property);
                }
            }
        }

        return properties;
    }

    private static object? CreateValue(object? value, Type declaredType, ValiantSettingsPropertySchema property, bool revealSecrets)
    {
        if (!revealSecrets && ShouldMask(property))
        {
            return Mask;
        }

        if (value is null)
        {
            return null;
        }

        if (property.Item is not null && value is IEnumerable enumerable && value is not string)
        {
            var itemType = declaredType.IsArray ? declaredType.GetElementType() : declaredType.GetInterfaces()
                .Prepend(declaredType)
                .FirstOrDefault(static candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];
            var items = new List<object?>();
            foreach (var item in enumerable)
            {
                items.Add(CreateValue(item, itemType ?? item?.GetType() ?? typeof(object), property.Item, revealSecrets));
            }

            return items;
        }

        if (property.Kind == ValiantSettingsPropertyKind.Object || property.Properties.Count > 0)
        {
            return CreateObject(value, Nullable.GetUnderlyingType(declaredType) ?? declaredType, property.Properties, revealSecrets);
        }

        // Never hand arbitrary objects to the JSON serializer: recursive or opaque
        // values have no complete schema with which to enforce secret masking.
        if (value is double doubleValue && !double.IsFinite(doubleValue) ||
            value is float singleValue && !float.IsFinite(singleValue) ||
            value is IntPtr or UIntPtr)
        {
            return null;
        }

        var type = value.GetType();
        return type.IsPrimitive || type.IsEnum || value is string or decimal or DateTime or
            DateTimeOffset or DateOnly or TimeOnly or TimeSpan or Guid or Uri
            ? value
            : null;
    }

    internal static bool ShouldMask(ValiantSettingsPropertySchema property) =>
        property.IsSecret || SensitiveSettingsNames.IsSensitive(property.Name, property.ConfigurationKey);
}
