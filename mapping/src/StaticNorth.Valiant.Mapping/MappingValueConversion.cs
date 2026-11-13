using System.Linq.Expressions;

namespace StaticNorth.Valiant.Mapping;

internal static class MappingValueConversion
{
    private static readonly Dictionary<(Type Source, Type Target), Func<object?, object?>?> Converters = [];

    public static bool TryConvert(object? value, Type source, Type target, out object? converted)
    {
        Func<object?, object?>? converter;
        lock (Converters)
        {
            if (!Converters.TryGetValue((source, target), out converter))
            {
                converter = CreateConverter(source, target);
                Converters.Add((source, target), converter);
            }
        }

        converted = converter?.Invoke(value);
        return converter is not null;
    }

    private static Func<object?, object?>? CreateConverter(Type source, Type target)
    {
        var sourceValue = Nullable.GetUnderlyingType(source);
        var targetValue = Nullable.GetUnderlyingType(target);
        if (CreateNativeIntegerConverter(source, target, sourceValue, targetValue) is { } nativeConverter)
        {
            return nativeConverter;
        }

        var input = Expression.Parameter(typeof(object));
        UnaryExpression conversion;
        try
        {
            conversion = Expression.Convert(Expression.Convert(input, source), target);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        if (conversion.Method?.Name != "op_Implicit" &&
            ((sourceValue is not null && targetValue is null) ||
                !IsImplicitNumericConversion(sourceValue ?? source, targetValue ?? target)))
        {
            return null;
        }

        return Expression.Lambda<Func<object?, object?>>(Expression.Convert(conversion, typeof(object)), input)
            .Compile(preferInterpretation: true);
    }

    private static Func<object?, object?>? CreateNativeIntegerConverter(Type source, Type target, Type? sourceValue, Type? targetValue)
    {
        if (sourceValue is not null && targetValue is null)
        {
            return null;
        }

        source = sourceValue ?? source;
        target = targetValue ?? target;
        if (source == typeof(IntPtr))
        {
            if (target == typeof(long)) return value => value is null ? null : (long)(IntPtr)value;
            if (target == typeof(float)) return value => value is null ? null : (float)(long)(IntPtr)value;
            if (target == typeof(double)) return value => value is null ? null : (double)(long)(IntPtr)value;
            if (target == typeof(decimal)) return value => value is null ? null : (decimal)(long)(IntPtr)value;
        }

        if (source == typeof(UIntPtr))
        {
            if (target == typeof(ulong)) return value => value is null ? null : (ulong)(UIntPtr)value;
            if (target == typeof(float)) return value => value is null ? null : (float)(ulong)(UIntPtr)value;
            if (target == typeof(double)) return value => value is null ? null : (double)(ulong)(UIntPtr)value;
            if (target == typeof(decimal)) return value => value is null ? null : (decimal)(ulong)(UIntPtr)value;
        }

        if (source.IsEnum)
        {
            return null;
        }

        var code = Type.GetTypeCode(source);
        if (target == typeof(IntPtr) && code is TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Char or TypeCode.Int32)
        {
            return value => value is null ? null : new IntPtr(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (target == typeof(UIntPtr) && code is TypeCode.Byte or TypeCode.UInt16 or TypeCode.Char or TypeCode.UInt32)
        {
            return value => value is null ? null : new UIntPtr(Convert.ToUInt32(value, System.Globalization.CultureInfo.InvariantCulture));
        }

        return null;
    }

    private static bool IsImplicitNumericConversion(Type source, Type target)
    {
        if (source == target)
        {
            return true;
        }

        if (source.IsEnum || target.IsEnum)
        {
            return false;
        }

        var destination = Type.GetTypeCode(target);
        return Type.GetTypeCode(source) switch
        {
            TypeCode.SByte => destination is TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.Byte => destination is TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.Int16 => destination is TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.Char => destination is TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.UInt16 => destination is TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.Int32 => destination is TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.UInt32 => destination is TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.Int64 or TypeCode.UInt64 => destination is TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.Single => destination is TypeCode.Double,
            _ => false
        };
    }
}
