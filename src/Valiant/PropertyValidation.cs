namespace Valiant;

/// <summary>
/// Fluent property validation builder used by Valiant validators.
/// </summary>
public struct PropertyValidation<T, TProperty>
{
    private readonly ValidationContext<T> _context;
    private readonly Func<T, TProperty> _property;
    private readonly string _propertyName;

    internal PropertyValidation(ValidationContext<T> context, Func<T, TProperty> property, string propertyName)
    {
        _context = context;
        _property = property;
        _propertyName = propertyName;
    }

    /// <summary>
    /// Requires the property value to be non-null.
    /// </summary>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> NotNull(string? code = null, string? message = null)
    {
        return Check("NotNull", IsNull(Value), "'" + _propertyName + "' must not be null.", code, message);
    }

    /// <summary>
    /// Requires the property value to be null.
    /// </summary>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> Null(string? code = null, string? message = null)
    {
        return Check("Null", !IsNull(Value), "'" + _propertyName + "' must be null.", code, message);
    }

    /// <summary>
    /// Requires a meaningful value. For strings, null, empty, and whitespace-only values fail; for other values, null or default values fail.
    /// </summary>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> Required(string? code = null, string? message = null)
    {
        var value = Value;
        var failed = value is string text
            ? string.IsNullOrWhiteSpace(text)
            : EqualityComparer<TProperty>.Default.Equals(value, default!);

        return Check("Required", failed, "'" + _propertyName + "' is required.", code, message);
    }

    /// <summary>
    /// Requires a non-empty value. For strings, null and empty values fail; whitespace-only values pass. For other values, null or default values fail.
    /// </summary>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> NotEmpty(string? code = null, string? message = null)
    {
        var value = Value;
        var failed = value switch
        {
            null => true,
            string text => text.Length == 0,
            _ => EqualityComparer<TProperty>.Default.Equals(value, default!)
        };

        return Check("NotEmpty", failed, "'" + _propertyName + "' must not be empty.", code, message);
    }

    /// <summary>
    /// Requires an empty value. For strings, null and empty values pass; whitespace-only values fail.
    /// </summary>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> Empty(string? code = null, string? message = null)
    {
        var value = Value;
        var failed = value is string text
            ? !string.IsNullOrEmpty(text)
            : !EqualityComparer<TProperty>.Default.Equals(value, default!);

        return Check("Empty", failed, "'" + _propertyName + "' must be empty.", code, message);
    }

    /// <summary>
    /// Requires a string value to have a length between <paramref name="min" /> and <paramref name="max" />, inclusive. Null values pass.
    /// </summary>
    /// <param name="min">The minimum allowed string length.</param>
    /// <param name="max">The maximum allowed string length.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> LengthBetween(int min, int max, string? code = null, string? message = null)
    {
        var length = (Value as string)?.Length;
        return Check("LengthBetween", length is not null && (length < min || length > max), "'" + _propertyName + "' must be between " + min + " and " + max + " characters.", code, message);
    }

    /// <summary>
    /// Requires a string value to have at least <paramref name="min" /> characters. Null values pass.
    /// </summary>
    /// <param name="min">The minimum allowed string length.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> MinLength(int min, string? code = null, string? message = null)
    {
        var length = (Value as string)?.Length;
        return Check("MinLength", length is not null && length < min, "'" + _propertyName + "' must be at least " + min + " characters.", code, message);
    }

    /// <summary>
    /// Requires a string value to have no more than <paramref name="max" /> characters. Null values pass.
    /// </summary>
    /// <param name="max">The maximum allowed string length.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> MaxLength(int max, string? code = null, string? message = null)
    {
        var length = (Value as string)?.Length;
        return Check("MaxLength", length is not null && length > max, "'" + _propertyName + "' must be at most " + max + " characters.", code, message);
    }

    /// <summary>
    /// Requires the property value to be greater than <paramref name="value" />.
    /// </summary>
    /// <param name="value">The exclusive lower bound.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> GreaterThan(TProperty value, string? code = null, string? message = null)
    {
        return Check("GreaterThan", Comparer<TProperty>.Default.Compare(Value, value) <= 0, "'" + _propertyName + "' must be greater than " + value + ".", code, message);
    }

    /// <summary>
    /// Requires the property value to be greater than or equal to <paramref name="value" />.
    /// </summary>
    /// <param name="value">The inclusive lower bound.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> GreaterThanOrEqualTo(TProperty value, string? code = null, string? message = null)
    {
        return Check("GreaterThanOrEqualTo", Comparer<TProperty>.Default.Compare(Value, value) < 0, "'" + _propertyName + "' must be greater than or equal to " + value + ".", code, message);
    }

    /// <summary>
    /// Requires the property value to be less than <paramref name="value" />.
    /// </summary>
    /// <param name="value">The exclusive upper bound.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> LessThan(TProperty value, string? code = null, string? message = null)
    {
        return Check("LessThan", Comparer<TProperty>.Default.Compare(Value, value) >= 0, "'" + _propertyName + "' must be less than " + value + ".", code, message);
    }

    /// <summary>
    /// Requires the property value to be less than or equal to <paramref name="value" />.
    /// </summary>
    /// <param name="value">The inclusive upper bound.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> LessThanOrEqualTo(TProperty value, string? code = null, string? message = null)
    {
        return Check("LessThanOrEqualTo", Comparer<TProperty>.Default.Compare(Value, value) > 0, "'" + _propertyName + "' must be less than or equal to " + value + ".", code, message);
    }

    /// <summary>
    /// Requires the property value to equal <paramref name="value" />.
    /// </summary>
    /// <param name="value">The expected value.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> Equal(TProperty value, string? code = null, string? message = null)
    {
        return Check("Equal", !EqualityComparer<TProperty>.Default.Equals(Value, value), "'" + _propertyName + "' must be equal to " + value + ".", code, message);
    }

    /// <summary>
    /// Requires the property value to not equal <paramref name="value" />.
    /// </summary>
    /// <param name="value">The disallowed value.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> NotEqual(TProperty value, string? code = null, string? message = null)
    {
        return Check("NotEqual", EqualityComparer<TProperty>.Default.Equals(Value, value), "'" + _propertyName + "' must not be equal to " + value + ".", code, message);
    }

    /// <summary>
    /// Requires the property value to be between <paramref name="from" /> and <paramref name="to" />, inclusive.
    /// </summary>
    /// <param name="from">The inclusive lower bound.</param>
    /// <param name="to">The inclusive upper bound.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> InclusiveBetween(TProperty from, TProperty to, string? code = null, string? message = null)
    {
        var comparer = Comparer<TProperty>.Default;
        var value = Value;
        return Check("InclusiveBetween", comparer.Compare(value, from) < 0 || comparer.Compare(value, to) > 0, "'" + _propertyName + "' must be between " + from + " and " + to + ".", code, message);
    }

    /// <summary>
    /// Requires the property value to be between <paramref name="from" /> and <paramref name="to" />, exclusive.
    /// </summary>
    /// <param name="from">The exclusive lower bound.</param>
    /// <param name="to">The exclusive upper bound.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> ExclusiveBetween(TProperty from, TProperty to, string? code = null, string? message = null)
    {
        var comparer = Comparer<TProperty>.Default;
        var value = Value;
        return Check("ExclusiveBetween", comparer.Compare(value, from) <= 0 || comparer.Compare(value, to) >= 0, "'" + _propertyName + "' must be between " + from + " and " + to + ", exclusive.", code, message);
    }

    /// <summary>
    /// Requires the property value to satisfy <paramref name="predicate" />.
    /// </summary>
    /// <param name="predicate">The predicate that must return true for the value to be valid.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> Satisfies(Func<TProperty, bool> predicate, string? code = null, string? message = null)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return Check("Satisfies", !predicate(Value), "'" + _propertyName + "' did not satisfy the custom validation predicate.", code, message);
    }

    /// <summary>
    /// Requires the model and property value to satisfy <paramref name="predicate" />.
    /// </summary>
    /// <param name="predicate">The predicate that must return true for the value to be valid.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> Satisfies(Func<T, TProperty, bool> predicate, string? code = null, string? message = null)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return Check("Satisfies", !predicate(_context.Instance, Value), "'" + _propertyName + "' did not satisfy the custom validation predicate.", code, message);
    }

    /// <summary>
    /// Requires the property value to satisfy an asynchronous <paramref name="predicate" />.
    /// </summary>
    /// <param name="predicate">The asynchronous predicate that must return true for the value to be valid.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> SatisfiesAsync(Func<TProperty, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<bool>> predicate, string? code = null, string? message = null)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        var context = _context;
        var property = _property;
        return AddAsync("SatisfiesAsync", "'" + _propertyName + "' did not satisfy the custom validation predicate.", cancellationToken => predicate(property(context.Instance), cancellationToken), code, message);
    }

    /// <summary>
    /// Requires the model and property value to satisfy an asynchronous <paramref name="predicate" />.
    /// </summary>
    /// <param name="predicate">The asynchronous predicate that must return true for the value to be valid.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> SatisfiesAsync(Func<T, TProperty, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<bool>> predicate, string? code = null, string? message = null)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        var context = _context;
        var property = _property;
        return AddAsync("SatisfiesAsync", "'" + _propertyName + "' did not satisfy the custom validation predicate.", cancellationToken => predicate(context.Instance, property(context.Instance), cancellationToken), code, message);
    }

    /// <summary>
    /// Requires the property value to satisfy an asynchronous <paramref name="predicate" />.
    /// </summary>
    /// <param name="predicate">The asynchronous predicate that must return true for the value to be valid.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> SatisfiesAsync(Func<TProperty, global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task<bool>> predicate, string? code = null, string? message = null)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        var context = _context;
        var property = _property;
        return AddAsync("SatisfiesAsync", "'" + _propertyName + "' did not satisfy the custom validation predicate.", async cancellationToken => await predicate(property(context.Instance), cancellationToken).ConfigureAwait(false), code, message);
    }

    /// <summary>
    /// Requires the model and property value to satisfy an asynchronous <paramref name="predicate" />.
    /// </summary>
    /// <param name="predicate">The asynchronous predicate that must return true for the value to be valid.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> SatisfiesAsync(Func<T, TProperty, global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task<bool>> predicate, string? code = null, string? message = null)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        var context = _context;
        var property = _property;
        return AddAsync("SatisfiesAsync", "'" + _propertyName + "' did not satisfy the custom validation predicate.", async cancellationToken => await predicate(context.Instance, property(context.Instance), cancellationToken).ConfigureAwait(false), code, message);
    }

    private TProperty Value => _property(_context.Instance);

    private PropertyValidation<T, TProperty> Check(string defaultCode, bool failed, string defaultMessage, string? code, string? message)
    {
        if (failed)
        {
            _context.AddFailure(_propertyName, code ?? defaultCode, message ?? defaultMessage);
        }

        return this;
    }

    private PropertyValidation<T, TProperty> AddAsync(
        string defaultCode,
        string defaultMessage,
        Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<bool>> predicate,
        string? code,
        string? message)
    {
        var context = _context;
        var propertyName = _propertyName;
        var errorCode = code ?? defaultCode;
        var errorMessage = message ?? defaultMessage;
        context.AddAsyncValidation(async cancellationToken =>
        {
            if (!await predicate(cancellationToken).ConfigureAwait(false))
            {
                context.AddFailure(propertyName, errorCode, errorMessage);
            }
        });

        return this;
    }

    private static bool IsNull(TProperty value)
    {
        return value is null;
    }
}
