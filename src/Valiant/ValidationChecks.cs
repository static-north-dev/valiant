namespace Valiant;

/// <summary>
/// Provides validation predicate helpers used by generated validators and runtime validation builders.
/// </summary>
public static class ValidationChecks
{
    /// <summary>
    /// Determines whether an enum value is defined by its enum type. Null values are treated as valid.
    /// </summary>
    /// <param name="value">The enum value to inspect.</param>
    /// <returns><c>true</c> when the value is null or defined by its enum type; otherwise, <c>false</c>.</returns>
    public static bool IsDefinedEnum<TValue>(TValue value)
    {
        if (value is null)
        {
            return true;
        }

        var enumType = global::System.Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        return enumType.IsEnum && global::System.Enum.IsDefined(enumType, value);
    }

    /// <summary>
    /// Determines whether a string value is a defined enum member name. Null values are treated as valid.
    /// </summary>
    /// <param name="value">The enum member name to inspect.</param>
    /// <returns><c>true</c> when the value is null or matches a defined enum member name; otherwise, <c>false</c>.</returns>
    public static bool IsEnumName<TEnum>(string? value)
        where TEnum : struct, global::System.Enum
    {
        return IsEnumName(typeof(TEnum), value);
    }

    /// <summary>
    /// Determines whether a string value is a defined enum member name. Null values are treated as valid.
    /// </summary>
    /// <param name="enumType">The enum type whose member names are allowed.</param>
    /// <param name="value">The enum member name to inspect.</param>
    /// <returns><c>true</c> when the value is null or matches a defined enum member name; otherwise, <c>false</c>.</returns>
    public static bool IsEnumName(Type enumType, string? value)
    {
        _ = enumType ?? throw new ArgumentNullException(nameof(enumType));
        if (!enumType.IsEnum)
        {
            throw new ArgumentException("The supplied type must be an enum.", nameof(enumType));
        }

        return value is null || global::System.Enum.IsDefined(enumType, value);
    }

    /// <summary>
    /// Determines whether an integer value maps to a defined enum member value. Null values are treated as valid.
    /// </summary>
    /// <param name="value">The integer value to inspect.</param>
    /// <returns><c>true</c> when the value is null or maps to a defined enum member value; otherwise, <c>false</c>.</returns>
    public static bool IsEnumValue<TEnum, TValue>(TValue value)
        where TEnum : struct, global::System.Enum
    {
        return IsEnumValue(typeof(TEnum), value);
    }

    /// <summary>
    /// Determines whether an integer value maps to a defined enum member value. Null values are treated as valid.
    /// </summary>
    /// <param name="enumType">The enum type whose values are allowed.</param>
    /// <param name="value">The integer value to inspect.</param>
    /// <returns><c>true</c> when the value is null or maps to a defined enum member value; otherwise, <c>false</c>.</returns>
    public static bool IsEnumValue<TValue>(Type enumType, TValue value)
    {
        _ = enumType ?? throw new ArgumentNullException(nameof(enumType));
        if (!enumType.IsEnum)
        {
            throw new ArgumentException("The supplied type must be an enum.", nameof(enumType));
        }

        var valueType = global::System.Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        if (!IsIntegerType(valueType))
        {
            return false;
        }

        if (value is null)
        {
            return true;
        }

        try
        {
            var underlyingType = global::System.Enum.GetUnderlyingType(enumType);
            var converted = global::System.Convert.ChangeType(value, underlyingType, global::System.Globalization.CultureInfo.InvariantCulture);
            return global::System.Enum.IsDefined(enumType, converted);
        }
        catch (global::System.ArgumentException)
        {
            return false;
        }
        catch (global::System.FormatException)
        {
            return false;
        }
        catch (global::System.InvalidCastException)
        {
            return false;
        }
        catch (global::System.OverflowException)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether a type is one of the CLR integer types that can back an enum value.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><c>true</c> when the type is an integer type; otherwise, <c>false</c>.</returns>
    internal static bool IsIntegerType(Type type)
    {
        _ = type ?? throw new ArgumentNullException(nameof(type));
        type = global::System.Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(byte) ||
            type == typeof(sbyte) ||
            type == typeof(short) ||
            type == typeof(ushort) ||
            type == typeof(int) ||
            type == typeof(uint) ||
            type == typeof(long) ||
            type == typeof(ulong);
    }

    /// <summary>
    /// Determines whether a value has a count. Strings, collections, and enumerable values are supported.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <param name="count">The discovered count when the method returns <c>true</c>.</param>
    /// <returns><c>true</c> when the value exposes a count or can be enumerated; otherwise, <c>false</c>.</returns>
    internal static bool TryGetCount<TValue>(TValue value, out int count)
    {
        switch (value)
        {
            case null:
                count = 0;
                return false;
            case string text:
                count = text.Length;
                return true;
            case global::System.Collections.ICollection collection:
                count = collection.Count;
                return true;
            case global::System.Collections.IEnumerable enumerable:
                count = 0;
                foreach (var _ in enumerable)
                {
                    count++;
                }

                return true;
            default:
                count = 0;
                return false;
        }
    }

    /// <summary>
    /// Determines whether a value is empty. Strings and collections are empty when their count is zero; other values are empty when they are null or default.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns><c>true</c> when the value is empty; otherwise, <c>false</c>.</returns>
    public static bool IsEmpty<TValue>(TValue value)
    {
        return value switch
        {
            null => true,
            string text => text.Length == 0,
            _ when TryGetCount(value, out var count) => count == 0,
            _ => global::System.Collections.Generic.EqualityComparer<TValue>.Default.Equals(value, default!)
        };
    }

    /// <summary>
    /// Determines whether a value has a count between two bounds, inclusive. Null values are treated as valid.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <param name="min">The minimum allowed count.</param>
    /// <param name="max">The maximum allowed count.</param>
    /// <returns><c>true</c> when the value is null or its count is within the range; otherwise, <c>false</c>.</returns>
    public static bool HasCountBetween<TValue>(TValue value, int min, int max)
    {
        return !TryGetCount(value, out var count) || (count >= min && count <= max);
    }

    /// <summary>
    /// Determines whether a value has exactly the specified count. Null values are treated as valid.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <param name="expected">The exact allowed count.</param>
    /// <returns><c>true</c> when the value is null or its count equals <paramref name="expected" />; otherwise, <c>false</c>.</returns>
    public static bool HasCount<TValue>(TValue value, int expected)
    {
        return !TryGetCount(value, out var count) || count == expected;
    }

    /// <summary>
    /// Determines whether a value has at least the specified count. Null values are treated as valid.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <param name="min">The minimum allowed count.</param>
    /// <returns><c>true</c> when the value is null or its count is at least <paramref name="min" />; otherwise, <c>false</c>.</returns>
    public static bool HasMinCount<TValue>(TValue value, int min)
    {
        return !TryGetCount(value, out var count) || count >= min;
    }

    /// <summary>
    /// Determines whether a value has no more than the specified count. Null values are treated as valid.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <param name="max">The maximum allowed count.</param>
    /// <returns><c>true</c> when the value is null or its count is at most <paramref name="max" />; otherwise, <c>false</c>.</returns>
    public static bool HasMaxCount<TValue>(TValue value, int max)
    {
        return !TryGetCount(value, out var count) || count <= max;
    }

    /// <summary>
    /// Determines whether a collection contains an expected item.
    /// </summary>
    /// <param name="value">The collection to inspect.</param>
    /// <param name="expected">The expected item.</param>
    /// <returns><c>true</c> when the collection contains the item; otherwise, <c>false</c>.</returns>
    public static bool Contains<TValue, TItem>(TValue value, TItem expected)
    {
        if (value is string text && expected is char character)
        {
            return text.IndexOf(character) >= 0;
        }

        if (value is global::System.Collections.Generic.IEnumerable<TItem> typedEnumerable)
        {
            foreach (var item in typedEnumerable)
            {
                if (global::System.Collections.Generic.EqualityComparer<TItem>.Default.Equals(item, expected))
                {
                    return true;
                }
            }

            return false;
        }

        if (value is global::System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is null && expected is null)
                {
                    return true;
                }

                if (item is TItem typedItem &&
                    global::System.Collections.Generic.EqualityComparer<TItem>.Default.Equals(typedItem, expected))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether all items in a collection satisfy a predicate. Null collections are treated as valid.
    /// </summary>
    /// <param name="value">The collection to inspect.</param>
    /// <param name="predicate">The predicate every item must satisfy.</param>
    /// <returns><c>true</c> when the collection is null or every item satisfies the predicate; otherwise, <c>false</c>.</returns>
    public static bool All<TValue, TItem>(TValue value, Func<TItem, bool> predicate)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        if (value is null)
        {
            return true;
        }

        foreach (var item in (global::System.Collections.Generic.IEnumerable<TItem>)value)
        {
            if (!predicate(item))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether at least one item in a collection satisfies a predicate.
    /// </summary>
    /// <param name="value">The collection to inspect.</param>
    /// <param name="predicate">The predicate at least one item must satisfy.</param>
    /// <returns><c>true</c> when at least one item satisfies the predicate; otherwise, <c>false</c>.</returns>
    public static bool Any<TValue, TItem>(TValue value, Func<TItem, bool> predicate)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        if (value is null)
        {
            return false;
        }

        foreach (var item in (global::System.Collections.Generic.IEnumerable<TItem>)value)
        {
            if (predicate(item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether no item in a collection satisfies a predicate. Null collections are treated as valid.
    /// </summary>
    /// <param name="value">The collection to inspect.</param>
    /// <param name="predicate">The predicate no item may satisfy.</param>
    /// <returns><c>true</c> when the collection is null or no item satisfies the predicate; otherwise, <c>false</c>.</returns>
    public static bool None<TValue, TItem>(TValue value, Func<TItem, bool> predicate)
    {
        return !Any(value, predicate);
    }

    /// <summary>
    /// Determines whether a dictionary contains an expected key.
    /// </summary>
    /// <param name="value">The dictionary to inspect.</param>
    /// <param name="key">The expected key.</param>
    /// <returns><c>true</c> when the dictionary contains the key; otherwise, <c>false</c>.</returns>
    public static bool HasKey<TValue, TKey>(TValue value, TKey key)
    {
        return value is global::System.Collections.IDictionary dictionary && dictionary.Contains(key!);
    }

    /// <summary>
    /// Determines whether a dictionary contains an expected value.
    /// </summary>
    /// <param name="value">The dictionary to inspect.</param>
    /// <param name="expected">The expected value.</param>
    /// <returns><c>true</c> when the dictionary contains the value; otherwise, <c>false</c>.</returns>
    public static bool HasValue<TValue, TExpected>(TValue value, TExpected expected)
    {
        if (value is not global::System.Collections.IDictionary dictionary)
        {
            return false;
        }

        foreach (var item in dictionary.Values)
        {
            if (item is null && expected is null)
            {
                return true;
            }

            if (item is TExpected typedItem &&
                global::System.Collections.Generic.EqualityComparer<TExpected>.Default.Equals(typedItem, expected))
            {
                return true;
            }
        }

        return false;
    }
}
