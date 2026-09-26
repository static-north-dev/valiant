using System.Collections;
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
        revealSecrets = includeRuntimeValues && canRevealSecrets && revealSecrets;

        var schemas = providers
            .Select(provider => CreateSchema(provider.Schema, serviceProvider, includeRuntimeValues, revealSecrets))
            .OrderBy(static schema => schema.SectionPath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static schema => schema.Name, StringComparer.Ordinal)
            .ToArray();

        return new ValiantSettingsSchemaDocument(schemas, canRevealSecrets, includeRuntimeValues && revealSecrets);
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
            [.. schema.Properties.Select(CreateProperty)],
            runtimeValues,
            runtimeValuesError);
    }

    private static ValiantSettingsSchemaPropertyEntry CreateProperty(ValiantSettingsPropertySchema property)
    {
        return new ValiantSettingsSchemaPropertyEntry(
            property.Name,
            property.ConfigurationKey,
            property.TypeName,
            property.Kind.ToString(),
            property.IsRequired,
            property.IsNullable,
            property.IsSecret,
            property.DefaultValue,
            property.Item is null ? null : CreateProperty(property.Item),
            [.. property.Properties.Select(CreateProperty)]);
    }
}

internal static class ValiantSettingsRuntimeValueFactory
{
    private const string Mask = "********";

    private static readonly string[] SensitiveNameParts =
    [
        "password",
        "secret",
        "token",
        "apikey",
        "api_key",
        "connectionstring",
        "connection_string",
        "clientsecret",
        "client_secret"
    ];

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

            var value = optionsType.GetProperty(nameof(IOptions<object>.Value))?.GetValue(options);
            if (value is null)
            {
                return null;
            }

            return CreateObject(value, schema.Properties, revealSecrets);
        }
        catch (Exception)
        {
            // Options factories, validators and getters can include credentials in exception messages.
            error = "Runtime values could not be loaded. Check the application's options configuration and validation.";
            return null;
        }
    }

    private static IReadOnlyDictionary<string, object?> CreateObject(
        object instance,
        IReadOnlyList<ValiantSettingsPropertySchema> properties,
        bool revealSecrets)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in properties)
        {
            if (!revealSecrets && ShouldMask(property))
            {
                values[property.ConfigurationKey] = Mask;
                continue;
            }

            var propertyInfo = instance.GetType().GetProperty(property.Name);
            if (propertyInfo is null)
            {
                continue;
            }

            var value = propertyInfo.GetValue(instance);
            values[property.ConfigurationKey] = CreateValue(value, property, revealSecrets);
        }

        return values;
    }

    private static object? CreateValue(object? value, ValiantSettingsPropertySchema property, bool revealSecrets)
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
            var items = new List<object?>();
            foreach (var item in enumerable)
            {
                items.Add(CreateValue(item, property.Item, revealSecrets));
            }

            return items;
        }

        if (property.Kind == ValiantSettingsPropertyKind.Object || property.Properties.Count > 0)
        {
            return CreateObject(value, property.Properties, revealSecrets);
        }

        // Never hand arbitrary objects to the JSON serializer: recursive or opaque
        // values have no complete schema with which to enforce secret masking.
        var type = value.GetType();
        return type.IsPrimitive || type.IsEnum || value is string or decimal or DateTime or
            DateTimeOffset or DateOnly or TimeOnly or TimeSpan or Guid or Uri
            ? value
            : null;
    }

    private static bool ShouldMask(ValiantSettingsPropertySchema property)
    {
        if (property.IsSecret)
        {
            return true;
        }

        var name = property.Name.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);
        var key = property.ConfigurationKey.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);

        foreach (var part in SensitiveNameParts)
        {
            if (name.Contains(part, StringComparison.OrdinalIgnoreCase) ||
                key.Contains(part, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
