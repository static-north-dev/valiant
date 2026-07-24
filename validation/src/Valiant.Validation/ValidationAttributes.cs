namespace Valiant.Validation;

/// <summary>
/// Base type for Valiant validation attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
public abstract class ValiantValidationAttribute : Attribute
{
    protected ValiantValidationAttribute()
    {
    }

    protected ValiantValidationAttribute(string? code)
    {
        Code = code;
    }

    protected ValiantValidationAttribute(string? code, string? message)
    {
        Code = code;
        Message = message;
    }

    protected ValiantValidationAttribute(string? code, string? message, ValiantSeverity severity)
    {
        Code = code;
        Message = message;
        Severity = severity;
    }

    /// <summary>
    /// Gets or sets the optional error code used when this check fails.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets the optional error message used when this check fails.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the severity level of the validation failure.
    /// </summary>
    public ValiantSeverity Severity { get; set; } = ValiantSeverity.Error;

    /// <summary>
    /// Gets or sets the static predicate method name that controls whether this check runs.
    /// </summary>
    public string? ConditionMethod { get; set; }

    /// <summary>
    /// Gets or sets the type that declares <see cref="ConditionMethod" />. Defaults to the model type.
    /// </summary>
    public Type? ConditionType { get; set; }
}

/// <summary>
/// Requires the annotated value to be non-null.
/// </summary>
public sealed class NotNullAttribute : ValiantValidationAttribute
{
    public NotNullAttribute() { }
    public NotNullAttribute(string? code) : base(code) { }
    public NotNullAttribute(string? code, string? message) : base(code, message) { }
    public NotNullAttribute(string? code, string? message, ValiantSeverity severity) : base(code, message, severity) { }
}

/// <summary>
/// Requires the annotated value to be null.
/// </summary>
public sealed class NullAttribute : ValiantValidationAttribute
{
    public NullAttribute() { }
    public NullAttribute(string? code) : base(code) { }
    public NullAttribute(string? code, string? message) : base(code, message) { }
    public NullAttribute(string? code, string? message, ValiantSeverity severity) : base(code, message, severity) { }
}

/// <summary>
/// Requires the annotated value to be present.
/// </summary>
public sealed class RequiredAttribute : ValiantValidationAttribute
{
    public RequiredAttribute() { }
    public RequiredAttribute(string? code) : base(code) { }
    public RequiredAttribute(string? code, string? message) : base(code, message) { }
    public RequiredAttribute(string? code, string? message, ValiantSeverity severity) : base(code, message, severity) { }
}

/// <summary>
/// Requires the annotated value to be non-empty.
/// </summary>
public sealed class NotEmptyAttribute : ValiantValidationAttribute
{
    public NotEmptyAttribute() { }
    public NotEmptyAttribute(string? code) : base(code) { }
    public NotEmptyAttribute(string? code, string? message) : base(code, message) { }
    public NotEmptyAttribute(string? code, string? message, ValiantSeverity severity) : base(code, message, severity) { }
}

/// <summary>
/// Requires the annotated value to be empty.
/// </summary>
public sealed class EmptyAttribute : ValiantValidationAttribute
{
    public EmptyAttribute() { }
    public EmptyAttribute(string? code) : base(code) { }
    public EmptyAttribute(string? code, string? message) : base(code, message) { }
    public EmptyAttribute(string? code, string? message, ValiantSeverity severity) : base(code, message, severity) { }
}

/// <summary>
/// Requires the annotated enum value to be defined by its enum type.
/// </summary>
public sealed class DefinedEnumAttribute : ValiantValidationAttribute
{
    public DefinedEnumAttribute() { }
    public DefinedEnumAttribute(string? code) : base(code) { }
    public DefinedEnumAttribute(string? code, string? message) : base(code, message) { }
    public DefinedEnumAttribute(string? code, string? message, ValiantSeverity severity) : base(code, message, severity) { }
}

/// <summary>
/// Reuses the specified property validator for the annotated value.
/// </summary>
/// <typeparam name="TValidator">The property validator type to use.</typeparam>
public sealed class UseValidatorAttribute<TValidator> : ValiantValidationAttribute
{
    public UseValidatorAttribute() { }
}

/// <summary>
/// Requires the annotated string value to match a name from the specified enum type.
/// </summary>
/// <param name="enumType">The enum type that supplies allowed names.</param>
public sealed class EnumNameAttribute(Type enumType) : ValiantValidationAttribute
{
    public EnumNameAttribute(Type enumType, string? code) : this(enumType) { Code = code; }
    public EnumNameAttribute(Type enumType, string? code, string? message) : this(enumType, code) { Message = message; }
    public EnumNameAttribute(Type enumType, string? code, string? message, ValiantSeverity severity) : this(enumType, code, message) { Severity = severity; }
    public Type EnumType { get; } = enumType;
}

/// <summary>
/// Requires the annotated value to match a value from the specified enum type.
/// </summary>
/// <param name="enumType">The enum type that supplies allowed values.</param>
public sealed class EnumValueAttribute(Type enumType) : ValiantValidationAttribute
{
    public EnumValueAttribute(Type enumType, string? code) : this(enumType) { Code = code; }
    public EnumValueAttribute(Type enumType, string? code, string? message) : this(enumType, code) { Message = message; }
    public EnumValueAttribute(Type enumType, string? code, string? message, ValiantSeverity severity) : this(enumType, code, message) { Severity = severity; }
    public Type EnumType { get; } = enumType;
}

/// <summary>
/// Requires the annotated value length to be between the supplied bounds, inclusive.
/// </summary>
/// <param name="min">The minimum allowed length.</param>
/// <param name="max">The maximum allowed length.</param>
public sealed class LengthBetweenAttribute(int min, int max) : ValiantValidationAttribute
{
    public LengthBetweenAttribute(int min, int max, string? code) : this(min, max) { Code = code; }
    public LengthBetweenAttribute(int min, int max, string? code, string? message) : this(min, max, code) { Message = message; }
    public LengthBetweenAttribute(int min, int max, string? code, string? message, ValiantSeverity severity) : this(min, max, code, message) { Severity = severity; }
    public int Min { get; } = min;

    public int Max { get; } = max;
}

/// <summary>
/// Requires the annotated value length to be at least the supplied minimum.
/// </summary>
/// <param name="min">The minimum allowed length.</param>
public sealed class MinLengthAttribute(int min) : ValiantValidationAttribute
{
    public MinLengthAttribute(int min, string? code) : this(min) { Code = code; }
    public MinLengthAttribute(int min, string? code, string? message) : this(min, code) { Message = message; }
    public MinLengthAttribute(int min, string? code, string? message, ValiantSeverity severity) : this(min, code, message) { Severity = severity; }
    public int Min { get; } = min;
}

/// <summary>
/// Requires the annotated value length to be no greater than the supplied maximum.
/// </summary>
/// <param name="max">The maximum allowed length.</param>
public sealed class MaxLengthAttribute(int max) : ValiantValidationAttribute
{
    public MaxLengthAttribute(int max, string? code) : this(max) { Code = code; }
    public MaxLengthAttribute(int max, string? code, string? message) : this(max, code) { Message = message; }
    public MaxLengthAttribute(int max, string? code, string? message, ValiantSeverity severity) : this(max, code, message) { Severity = severity; }
    public int Max { get; } = max;
}

/// <summary>
/// Requires the annotated collection count to equal the supplied value.
/// </summary>
/// <param name="count">The required collection count.</param>
public sealed class CountAttribute(int count) : ValiantValidationAttribute
{
    public CountAttribute(int count, string? code) : this(count) { Code = code; }
    public CountAttribute(int count, string? code, string? message) : this(count, code) { Message = message; }
    public CountAttribute(int count, string? code, string? message, ValiantSeverity severity) : this(count, code, message) { Severity = severity; }
    public int Count { get; } = count;
}

/// <summary>
/// Requires the annotated collection count to be between the supplied bounds, inclusive.
/// </summary>
/// <param name="min">The minimum allowed count.</param>
/// <param name="max">The maximum allowed count.</param>
public sealed class CountBetweenAttribute(int min, int max) : ValiantValidationAttribute
{
    public CountBetweenAttribute(int min, int max, string? code) : this(min, max) { Code = code; }
    public CountBetweenAttribute(int min, int max, string? code, string? message) : this(min, max, code) { Message = message; }
    public CountBetweenAttribute(int min, int max, string? code, string? message, ValiantSeverity severity) : this(min, max, code, message) { Severity = severity; }
    public int Min { get; } = min;

    public int Max { get; } = max;
}

/// <summary>
/// Requires the annotated collection count to be at least the supplied minimum.
/// </summary>
/// <param name="min">The minimum allowed count.</param>
public sealed class MinCountAttribute(int min) : ValiantValidationAttribute
{
    public MinCountAttribute(int min, string? code) : this(min) { Code = code; }
    public MinCountAttribute(int min, string? code, string? message) : this(min, code) { Message = message; }
    public MinCountAttribute(int min, string? code, string? message, ValiantSeverity severity) : this(min, code, message) { Severity = severity; }
    public int Min { get; } = min;
}

/// <summary>
/// Requires the annotated collection count to be no greater than the supplied maximum.
/// </summary>
/// <param name="max">The maximum allowed count.</param>
public sealed class MaxCountAttribute(int max) : ValiantValidationAttribute
{
    public MaxCountAttribute(int max, string? code) : this(max) { Code = code; }
    public MaxCountAttribute(int max, string? code, string? message) : this(max, code) { Message = message; }
    public MaxCountAttribute(int max, string? code, string? message, ValiantSeverity severity) : this(max, code, message) { Severity = severity; }
    public int Max { get; } = max;
}

/// <summary>
/// Requires the annotated value to contain the supplied value.
/// </summary>
/// <param name="value">The value that must be present.</param>
public sealed class ContainsAttribute(string value) : ValiantValidationAttribute
{
    public ContainsAttribute(string value, string? code) : this(value) { Code = code; }
    public ContainsAttribute(string value, string? code, string? message) : this(value, code) { Message = message; }
    public ContainsAttribute(string value, string? code, string? message, ValiantSeverity severity) : this(value, code, message) { Severity = severity; }
    public string Value { get; } = value;
}

/// <summary>
/// Requires the annotated value to exclude the supplied value.
/// </summary>
/// <param name="value">The value that must be absent.</param>
public sealed class ExcludesAttribute(string value) : ValiantValidationAttribute
{
    public ExcludesAttribute(string value, string? code) : this(value) { Code = code; }
    public ExcludesAttribute(string value, string? code, string? message) : this(value, code) { Message = message; }
    public ExcludesAttribute(string value, string? code, string? message, ValiantSeverity severity) : this(value, code, message) { Severity = severity; }
    public string Value { get; } = value;
}

/// <summary>
/// Requires the annotated dictionary to contain the supplied key.
/// </summary>
/// <param name="key">The key that must be present.</param>
public sealed class HasKeyAttribute(string key) : ValiantValidationAttribute
{
    public HasKeyAttribute(string key, string? code) : this(key) { Code = code; }
    public HasKeyAttribute(string key, string? code, string? message) : this(key, code) { Message = message; }
    public HasKeyAttribute(string key, string? code, string? message, ValiantSeverity severity) : this(key, code, message) { Severity = severity; }
    public string Key { get; } = key;
}

/// <summary>
/// Requires the annotated dictionary to contain the supplied value.
/// </summary>
/// <param name="value">The value that must be present.</param>
public sealed class HasValueAttribute(string value) : ValiantValidationAttribute
{
    public HasValueAttribute(string value, string? code) : this(value) { Code = code; }
    public HasValueAttribute(string value, string? code, string? message) : this(value, code) { Message = message; }
    public HasValueAttribute(string value, string? code, string? message, ValiantSeverity severity) : this(value, code, message) { Severity = severity; }
    public string Value { get; } = value;
}

/// <summary>
/// Requires the annotated value to be greater than the supplied value.
/// </summary>
/// <param name="value">The exclusive lower bound.</param>
public sealed class GreaterThanAttribute(int value) : ValiantValidationAttribute
{
    public GreaterThanAttribute(int value, string? code) : this(value) { Code = code; }
    public GreaterThanAttribute(int value, string? code, string? message) : this(value, code) { Message = message; }
    public GreaterThanAttribute(int value, string? code, string? message, ValiantSeverity severity) : this(value, code, message) { Severity = severity; }
    public int Value { get; } = value;
}

/// <summary>
/// Requires the annotated value to be greater than or equal to the supplied value.
/// </summary>
/// <param name="value">The inclusive lower bound.</param>
public sealed class GreaterThanOrEqualToAttribute(int value) : ValiantValidationAttribute
{
    public GreaterThanOrEqualToAttribute(int value, string? code) : this(value) { Code = code; }
    public GreaterThanOrEqualToAttribute(int value, string? code, string? message) : this(value, code) { Message = message; }
    public GreaterThanOrEqualToAttribute(int value, string? code, string? message, ValiantSeverity severity) : this(value, code, message) { Severity = severity; }
    public int Value { get; } = value;
}

/// <summary>
/// Requires the annotated value to be less than the supplied value.
/// </summary>
/// <param name="value">The exclusive upper bound.</param>
public sealed class LessThanAttribute(int value) : ValiantValidationAttribute
{
    public LessThanAttribute(int value, string? code) : this(value) { Code = code; }
    public LessThanAttribute(int value, string? code, string? message) : this(value, code) { Message = message; }
    public LessThanAttribute(int value, string? code, string? message, ValiantSeverity severity) : this(value, code, message) { Severity = severity; }
    public int Value { get; } = value;
}

/// <summary>
/// Requires the annotated value to be less than or equal to the supplied value.
/// </summary>
/// <param name="value">The inclusive upper bound.</param>
public sealed class LessThanOrEqualToAttribute(int value) : ValiantValidationAttribute
{
    public LessThanOrEqualToAttribute(int value, string? code) : this(value) { Code = code; }
    public LessThanOrEqualToAttribute(int value, string? code, string? message) : this(value, code) { Message = message; }
    public LessThanOrEqualToAttribute(int value, string? code, string? message, ValiantSeverity severity) : this(value, code, message) { Severity = severity; }
    public int Value { get; } = value;
}

/// <summary>
/// Requires the annotated value to equal the supplied value.
/// </summary>
/// <param name="value">The expected value.</param>
public sealed class EqualAttribute(string value) : ValiantValidationAttribute
{
    public EqualAttribute(string value, string? code) : this(value) { Code = code; }
    public EqualAttribute(string value, string? code, string? message) : this(value, code) { Message = message; }
    public EqualAttribute(string value, string? code, string? message, ValiantSeverity severity) : this(value, code, message) { Severity = severity; }
    public string Value { get; } = value;
}

/// <summary>
/// Requires the annotated value to differ from the supplied value.
/// </summary>
/// <param name="value">The disallowed value.</param>
public sealed class NotEqualAttribute(string value) : ValiantValidationAttribute
{
    public NotEqualAttribute(string value, string? code) : this(value) { Code = code; }
    public NotEqualAttribute(string value, string? code, string? message) : this(value, code) { Message = message; }
    public NotEqualAttribute(string value, string? code, string? message, ValiantSeverity severity) : this(value, code, message) { Severity = severity; }
    public string Value { get; } = value;
}

/// <summary>
/// Requires the annotated value to be between the supplied bounds, inclusive.
/// </summary>
/// <param name="min">The inclusive lower bound.</param>
/// <param name="max">The inclusive upper bound.</param>
public sealed class InclusiveBetweenAttribute(int min, int max) : ValiantValidationAttribute
{
    public InclusiveBetweenAttribute(int min, int max, string? code) : this(min, max) { Code = code; }
    public InclusiveBetweenAttribute(int min, int max, string? code, string? message) : this(min, max, code) { Message = message; }
    public InclusiveBetweenAttribute(int min, int max, string? code, string? message, ValiantSeverity severity) : this(min, max, code, message) { Severity = severity; }
    public int Min { get; } = min;

    public int Max { get; } = max;
}

/// <summary>
/// Requires the annotated value to be between the supplied bounds, exclusive.
/// </summary>
/// <param name="min">The exclusive lower bound.</param>
/// <param name="max">The exclusive upper bound.</param>
public sealed class ExclusiveBetweenAttribute(int min, int max) : ValiantValidationAttribute
{
    public ExclusiveBetweenAttribute(int min, int max, string? code) : this(min, max) { Code = code; }
    public ExclusiveBetweenAttribute(int min, int max, string? code, string? message) : this(min, max, code) { Message = message; }
    public ExclusiveBetweenAttribute(int min, int max, string? code, string? message, ValiantSeverity severity) : this(min, max, code, message) { Severity = severity; }
    public int Min { get; } = min;

    public int Max { get; } = max;
}
