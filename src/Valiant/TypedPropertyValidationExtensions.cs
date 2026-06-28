namespace Valiant;

/// <summary>
/// Provides type-constrained validation methods for strings, enums, comparable values, collections, and dictionaries.
/// </summary>
public static class TypedPropertyValidationExtensions
{
    /// <summary>
    /// Requires the reference property value to be non-null.
    /// </summary>
    /// <param name="property">The reference property validation builder.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TProperty> NotNull<T, TProperty>(this PropertyValidation<T, TProperty> property, string? code = null, string? message = null)
        where TProperty : class?
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("NotNull", property.Value is null, "'" + property.PropertyName + "' must not be null.", code, message);
    }

    /// <summary>
    /// Requires the nullable value property to be non-null.
    /// </summary>
    /// <param name="property">The nullable value property validation builder.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TProperty?> NotNull<T, TProperty>(this PropertyValidation<T, TProperty?> property, string? code = null, string? message = null)
        where TProperty : struct
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("NotNull", property.Value is null, "'" + property.PropertyName + "' must not be null.", code, message);
    }

    /// <summary>
    /// Requires the reference property value to be null.
    /// </summary>
    /// <param name="property">The reference property validation builder.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TProperty> Null<T, TProperty>(this PropertyValidation<T, TProperty> property, string? code = null, string? message = null)
        where TProperty : class?
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("Null", property.Value is not null, "'" + property.PropertyName + "' must be null.", code, message);
    }

    /// <summary>
    /// Requires the nullable value property to be null.
    /// </summary>
    /// <param name="property">The nullable value property validation builder.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TProperty?> Null<T, TProperty>(this PropertyValidation<T, TProperty?> property, string? code = null, string? message = null)
        where TProperty : struct
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("Null", property.Value is not null, "'" + property.PropertyName + "' must be null.", code, message);
    }

    /// <summary>
    /// Requires the enum value to be defined by its enum type.
    /// </summary>
    /// <param name="property">The enum property validation builder.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TEnum> DefinedEnum<T, TEnum>(this PropertyValidation<T, TEnum> property, string? code = null, string? message = null)
        where TEnum : struct, Enum
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("DefinedEnum", !ValidationChecks.IsDefinedEnum(property.Value), "'" + property.PropertyName + "' must be a defined enum value.", code, message);
    }

    /// <summary>
    /// Requires the nullable enum value to be null or defined by its enum type.
    /// </summary>
    /// <param name="property">The nullable enum property validation builder.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TEnum?> DefinedEnum<T, TEnum>(this PropertyValidation<T, TEnum?> property, string? code = null, string? message = null)
        where TEnum : struct, Enum
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("DefinedEnum", !ValidationChecks.IsDefinedEnum(property.Value), "'" + property.PropertyName + "' must be a defined enum value.", code, message);
    }

    /// <summary>
    /// Requires a string value to match a defined enum member name. Null values pass.
    /// </summary>
    /// <param name="property">The string property validation builder.</param>
    /// <param name="enumType">The enum type whose member names are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, string?> EnumName<T>(this PropertyValidation<T, string?> property, Type enumType, string? code = null, string? message = null)
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("EnumName", !ValidationChecks.IsEnumName(enumType, property.Value), "'" + property.PropertyName + "' must be a valid enum name.", code, message);
    }

    /// <summary>
    /// Requires an integer value to map to a defined enum member value.
    /// </summary>
    /// <param name="property">The integer property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, int> EnumValue<T>(this PropertyValidation<T, int> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires a nullable integer value to be null or map to a defined enum member value.
    /// </summary>
    /// <param name="property">The nullable integer property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, int?> EnumValue<T>(this PropertyValidation<T, int?> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires a long integer value to map to a defined enum member value.
    /// </summary>
    /// <param name="property">The long integer property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, long> EnumValue<T>(this PropertyValidation<T, long> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires a nullable long integer value to be null or map to a defined enum member value.
    /// </summary>
    /// <param name="property">The nullable long integer property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, long?> EnumValue<T>(this PropertyValidation<T, long?> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires a short integer value to map to a defined enum member value.
    /// </summary>
    /// <param name="property">The short integer property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, short> EnumValue<T>(this PropertyValidation<T, short> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires a nullable short integer value to be null or map to a defined enum member value.
    /// </summary>
    /// <param name="property">The nullable short integer property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, short?> EnumValue<T>(this PropertyValidation<T, short?> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires a byte value to map to a defined enum member value.
    /// </summary>
    /// <param name="property">The byte property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, byte> EnumValue<T>(this PropertyValidation<T, byte> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires a nullable byte value to be null or map to a defined enum member value.
    /// </summary>
    /// <param name="property">The nullable byte property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, byte?> EnumValue<T>(this PropertyValidation<T, byte?> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires an unsigned integer value to map to a defined enum member value.
    /// </summary>
    /// <param name="property">The unsigned integer property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, uint> EnumValue<T>(this PropertyValidation<T, uint> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires a nullable unsigned integer value to be null or map to a defined enum member value.
    /// </summary>
    /// <param name="property">The nullable unsigned integer property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, uint?> EnumValue<T>(this PropertyValidation<T, uint?> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires an unsigned long integer value to map to a defined enum member value.
    /// </summary>
    /// <param name="property">The unsigned long integer property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, ulong> EnumValue<T>(this PropertyValidation<T, ulong> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires a nullable unsigned long integer value to be null or map to a defined enum member value.
    /// </summary>
    /// <param name="property">The nullable unsigned long integer property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, ulong?> EnumValue<T>(this PropertyValidation<T, ulong?> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires an unsigned short integer value to map to a defined enum member value.
    /// </summary>
    /// <param name="property">The unsigned short integer property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, ushort> EnumValue<T>(this PropertyValidation<T, ushort> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires a nullable unsigned short integer value to be null or map to a defined enum member value.
    /// </summary>
    /// <param name="property">The nullable unsigned short integer property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, ushort?> EnumValue<T>(this PropertyValidation<T, ushort?> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires a signed byte value to map to a defined enum member value.
    /// </summary>
    /// <param name="property">The signed byte property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, sbyte> EnumValue<T>(this PropertyValidation<T, sbyte> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires a nullable signed byte value to be null or map to a defined enum member value.
    /// </summary>
    /// <param name="property">The nullable signed byte property validation builder.</param>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, sbyte?> EnumValue<T>(this PropertyValidation<T, sbyte?> property, Type enumType, string? code = null, string? message = null)
    {
        return EnumValueCore(property, enumType, code, message);
    }

    /// <summary>
    /// Requires a string value to have a length between <paramref name="min" /> and <paramref name="max" />, inclusive. Null values pass.
    /// </summary>
    /// <param name="property">The string property validation builder.</param>
    /// <param name="min">The minimum allowed string length.</param>
    /// <param name="max">The maximum allowed string length.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, string?> LengthBetween<T>(this PropertyValidation<T, string?> property, int min, int max, string? code = null, string? message = null)
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        var length = property.Value?.Length;
        return property.Check("LengthBetween", length is not null && (length < min || length > max), "'" + property.PropertyName + "' must be between " + min + " and " + max + " characters.", code, message);
    }

    /// <summary>
    /// Requires a string value to have at least <paramref name="min" /> characters. Null values pass.
    /// </summary>
    /// <param name="property">The string property validation builder.</param>
    /// <param name="min">The minimum allowed string length.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, string?> MinLength<T>(this PropertyValidation<T, string?> property, int min, string? code = null, string? message = null)
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        var length = property.Value?.Length;
        return property.Check("MinLength", length is not null && length < min, "'" + property.PropertyName + "' must be at least " + min + " characters.", code, message);
    }

    /// <summary>
    /// Requires a string value to have no more than <paramref name="max" /> characters. Null values pass.
    /// </summary>
    /// <param name="property">The string property validation builder.</param>
    /// <param name="max">The maximum allowed string length.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, string?> MaxLength<T>(this PropertyValidation<T, string?> property, int max, string? code = null, string? message = null)
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        var length = property.Value?.Length;
        return property.Check("MaxLength", length is not null && length > max, "'" + property.PropertyName + "' must be at most " + max + " characters.", code, message);
    }

    /// <summary>
    /// Requires a collection or dictionary to have a count between <paramref name="min" /> and <paramref name="max" />, inclusive. Null values pass.
    /// </summary>
    /// <param name="property">The collection property validation builder.</param>
    /// <param name="min">The minimum allowed count.</param>
    /// <param name="max">The maximum allowed count.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TCollection> CountBetween<T, TCollection>(this PropertyValidation<T, TCollection> property, int min, int max, string? code = null, string? message = null)
        where TCollection : System.Collections.ICollection?
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("CountBetween", !ValidationChecks.HasCountBetween(property.Value, min, max), "'" + property.PropertyName + "' must contain between " + min + " and " + max + " item(s).", code, message);
    }

    /// <summary>
    /// Requires a collection or dictionary to have at least <paramref name="min" /> item(s). Null values pass.
    /// </summary>
    /// <param name="property">The collection property validation builder.</param>
    /// <param name="min">The minimum allowed count.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TCollection> MinCount<T, TCollection>(this PropertyValidation<T, TCollection> property, int min, string? code = null, string? message = null)
        where TCollection : System.Collections.ICollection?
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("MinCount", !ValidationChecks.HasMinCount(property.Value, min), "'" + property.PropertyName + "' must contain at least " + min + " item(s).", code, message);
    }

    /// <summary>
    /// Requires a collection or dictionary to have no more than <paramref name="max" /> item(s). Null values pass.
    /// </summary>
    /// <param name="property">The collection property validation builder.</param>
    /// <param name="max">The maximum allowed count.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TCollection> MaxCount<T, TCollection>(this PropertyValidation<T, TCollection> property, int max, string? code = null, string? message = null)
        where TCollection : System.Collections.ICollection?
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("MaxCount", !ValidationChecks.HasMaxCount(property.Value, max), "'" + property.PropertyName + "' must contain at most " + max + " item(s).", code, message);
    }

    /// <summary>
    /// Requires a collection to contain <paramref name="item" />.
    /// </summary>
    /// <param name="property">The collection property validation builder.</param>
    /// <param name="item">The item that must be present.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TCollection> Contains<T, TCollection, TItem>(this PropertyValidation<T, TCollection> property, TItem item, string? code = null, string? message = null)
        where TCollection : IEnumerable<TItem>?
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("Contains", !ValidationChecks.Contains(property.Value, item), "'" + property.PropertyName + "' must contain the expected item.", code, message);
    }

    /// <summary>
    /// Requires a collection to not contain <paramref name="item" />.
    /// </summary>
    /// <param name="property">The collection property validation builder.</param>
    /// <param name="item">The item that must be absent.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TCollection> Excludes<T, TCollection, TItem>(this PropertyValidation<T, TCollection> property, TItem item, string? code = null, string? message = null)
        where TCollection : IEnumerable<TItem>?
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("Excludes", ValidationChecks.Contains(property.Value, item), "'" + property.PropertyName + "' must not contain the excluded item.", code, message);
    }

    /// <summary>
    /// Requires a dictionary to contain <paramref name="key" />.
    /// </summary>
    /// <param name="property">The dictionary property validation builder.</param>
    /// <param name="key">The key that must be present.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TDictionary> HasKey<T, TDictionary, TKey>(this PropertyValidation<T, TDictionary> property, TKey key, string? code = null, string? message = null)
        where TDictionary : System.Collections.IDictionary?
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("HasKey", !ValidationChecks.HasKey(property.Value, key), "'" + property.PropertyName + "' must contain the expected key.", code, message);
    }

    /// <summary>
    /// Requires a dictionary to contain <paramref name="value" />.
    /// </summary>
    /// <param name="property">The dictionary property validation builder.</param>
    /// <param name="value">The value that must be present.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TDictionary> HasValue<T, TDictionary, TValue>(this PropertyValidation<T, TDictionary> property, TValue value, string? code = null, string? message = null)
        where TDictionary : System.Collections.IDictionary?
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("HasValue", !ValidationChecks.HasValue(property.Value, value), "'" + property.PropertyName + "' must contain the expected value.", code, message);
    }

    /// <summary>
    /// Requires the property value to be greater than <paramref name="value" />.
    /// </summary>
    /// <param name="property">The comparable property validation builder.</param>
    /// <param name="value">The exclusive lower bound.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TProperty> GreaterThan<T, TProperty>(this PropertyValidation<T, TProperty> property, TProperty value, string? code = null, string? message = null)
        where TProperty : IComparable<TProperty>
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("GreaterThan", property.Value.CompareTo(value) <= 0, "'" + property.PropertyName + "' must be greater than " + value + ".", code, message);
    }

    /// <summary>
    /// Requires the property value to be greater than or equal to <paramref name="value" />.
    /// </summary>
    /// <param name="property">The comparable property validation builder.</param>
    /// <param name="value">The inclusive lower bound.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TProperty> GreaterThanOrEqualTo<T, TProperty>(this PropertyValidation<T, TProperty> property, TProperty value, string? code = null, string? message = null)
        where TProperty : IComparable<TProperty>
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("GreaterThanOrEqualTo", property.Value.CompareTo(value) < 0, "'" + property.PropertyName + "' must be greater than or equal to " + value + ".", code, message);
    }

    /// <summary>
    /// Requires the property value to be less than <paramref name="value" />.
    /// </summary>
    /// <param name="property">The comparable property validation builder.</param>
    /// <param name="value">The exclusive upper bound.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TProperty> LessThan<T, TProperty>(this PropertyValidation<T, TProperty> property, TProperty value, string? code = null, string? message = null)
        where TProperty : IComparable<TProperty>
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("LessThan", property.Value.CompareTo(value) >= 0, "'" + property.PropertyName + "' must be less than " + value + ".", code, message);
    }

    /// <summary>
    /// Requires the property value to be less than or equal to <paramref name="value" />.
    /// </summary>
    /// <param name="property">The comparable property validation builder.</param>
    /// <param name="value">The inclusive upper bound.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TProperty> LessThanOrEqualTo<T, TProperty>(this PropertyValidation<T, TProperty> property, TProperty value, string? code = null, string? message = null)
        where TProperty : IComparable<TProperty>
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("LessThanOrEqualTo", property.Value.CompareTo(value) > 0, "'" + property.PropertyName + "' must be less than or equal to " + value + ".", code, message);
    }

    /// <summary>
    /// Requires the property value to be between <paramref name="from" /> and <paramref name="to" />, inclusive.
    /// </summary>
    /// <param name="property">The comparable property validation builder.</param>
    /// <param name="from">The inclusive lower bound.</param>
    /// <param name="to">The inclusive upper bound.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TProperty> InclusiveBetween<T, TProperty>(this PropertyValidation<T, TProperty> property, TProperty from, TProperty to, string? code = null, string? message = null)
        where TProperty : IComparable<TProperty>
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        var value = property.Value;
        return property.Check("InclusiveBetween", value.CompareTo(from) < 0 || value.CompareTo(to) > 0, "'" + property.PropertyName + "' must be between " + from + " and " + to + ".", code, message);
    }

    /// <summary>
    /// Requires the property value to be between <paramref name="from" /> and <paramref name="to" />, exclusive.
    /// </summary>
    /// <param name="property">The comparable property validation builder.</param>
    /// <param name="from">The exclusive lower bound.</param>
    /// <param name="to">The exclusive upper bound.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TProperty> ExclusiveBetween<T, TProperty>(this PropertyValidation<T, TProperty> property, TProperty from, TProperty to, string? code = null, string? message = null)
        where TProperty : IComparable<TProperty>
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        var value = property.Value;
        return property.Check("ExclusiveBetween", value.CompareTo(from) <= 0 || value.CompareTo(to) >= 0, "'" + property.PropertyName + "' must be between " + from + " and " + to + ", exclusive.", code, message);
    }

    private static PropertyValidation<T, TValue> EnumValueCore<T, TValue>(PropertyValidation<T, TValue> property, Type enumType, string? code, string? message)
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("EnumValue", !ValidationChecks.IsEnumValue(enumType, property.Value), "'" + property.PropertyName + "' must be a valid enum value.", code, message);
    }
}
