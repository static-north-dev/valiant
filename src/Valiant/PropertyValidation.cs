namespace Valiant;

/// <summary>
/// Fluent property validation builder used by Valiant validators.
/// </summary>
public struct PropertyValidation<T, TProperty>
{
    private readonly ValidationContext<T> _context;
    private readonly Func<T, TProperty> _property;
    private readonly string _propertyName;
    private readonly ValiantFailureBehavior? _failureBehavior;
    private readonly PropertyValidationState _state;

    internal PropertyValidation(ValidationContext<T> context, Func<T, TProperty> property, string propertyName)
        : this(context, property, propertyName, null, new PropertyValidationState())
    {
    }

    private PropertyValidation(
        ValidationContext<T> context,
        Func<T, TProperty> property,
        string propertyName,
        ValiantFailureBehavior? failureBehavior,
        PropertyValidationState state)
    {
        _context = context;
        _property = property;
        _propertyName = propertyName;
        _failureBehavior = failureBehavior;
        _state = state;
    }

    /// <summary>
    /// Stops this property chain after the first failed check.
    /// </summary>
    public PropertyValidation<T, TProperty> FailFast()
    {
        return new PropertyValidation<T, TProperty>(_context, _property, _propertyName, ValiantFailureBehavior.FailFast, _state);
    }

    /// <summary>
    /// Runs every check in this property chain even when an earlier check failed.
    /// </summary>
    public PropertyValidation<T, TProperty> ValidateAll()
    {
        return new PropertyValidation<T, TProperty>(_context, _property, _propertyName, ValiantFailureBehavior.ValidateAll, _state);
    }

    /// <summary>
    /// Reuses a validator for the property value. Null property values are skipped; use <see cref="Required" /> when the property must be present.
    /// </summary>
    /// <param name="validator">The validator for the property value.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> UseValidator<TValidatedProperty>(IValidator<TValidatedProperty> validator)
    {
        _ = validator ?? throw new ArgumentNullException(nameof(validator));
        if (ShouldSkip)
        {
            return this;
        }

        var context = _context;
        var property = _property;
        var propertyName = _propertyName;
        var state = _state;
        var failureBehavior = EffectiveFailureBehavior;

        context.AddDeferredValidation(
            () =>
            {
                if (context.ShouldStopValidation ||
                    (state.HasFailure && failureBehavior == ValiantFailureBehavior.FailFast))
                {
                    return;
                }

                var rawValue = property(context.Instance);
                if (rawValue is null)
                {
                    return;
                }

                if (rawValue is not TValidatedProperty value)
                {
                    throw new InvalidOperationException("The supplied property validator cannot validate property '" + propertyName + "'.");
                }

                var result = validator.Validate(value);
                AddValidatorFailures(context, propertyName, result, state);
            },
            async cancellationToken =>
            {
                if (context.ShouldStopValidation ||
                    (state.HasFailure && failureBehavior == ValiantFailureBehavior.FailFast))
                {
                    return;
                }

                var rawValue = property(context.Instance);
                if (rawValue is null)
                {
                    return;
                }

                if (rawValue is not TValidatedProperty value)
                {
                    throw new InvalidOperationException("The supplied property validator cannot validate property '" + propertyName + "'.");
                }

                var result = await validator.ValidateAsync(value, cancellationToken).ConfigureAwait(false);
                AddValidatorFailures(context, propertyName, result, state);
            });

        return this;
    }

    /// <summary>
    /// Requires a meaningful value. For strings, null, empty, and whitespace-only values fail; for other values, null or default values fail.
    /// </summary>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> Required(string? code = null, string? message = null)
    {
        if (ShouldSkip)
        {
            return this;
        }

        var value = Value;
        var failed = value is string text
            ? string.IsNullOrWhiteSpace(text)
            : EqualityComparer<TProperty>.Default.Equals(value, default!);

        return Check("Required", failed, "'" + _propertyName + "' is required.", code, message);
    }

    /// <summary>
    /// Requires a non-empty value. For strings and collections, null and empty values fail; whitespace-only strings pass. For other values, null or default values fail.
    /// </summary>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> NotEmpty(string? code = null, string? message = null)
    {
        if (ShouldSkip)
        {
            return this;
        }

        return Check("NotEmpty", ValidationChecks.IsEmpty(Value), "'" + _propertyName + "' must not be empty.", code, message);
    }

    /// <summary>
    /// Requires an empty value. For strings and collections, null and empty values pass; whitespace-only strings fail.
    /// </summary>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> Empty(string? code = null, string? message = null)
    {
        if (ShouldSkip)
        {
            return this;
        }

        return Check("Empty", !ValidationChecks.IsEmpty(Value), "'" + _propertyName + "' must be empty.", code, message);
    }

    /// <summary>
    /// Requires a string value to match a defined enum member name of <typeparamref name="TEnum" />. Null values pass.
    /// </summary>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> EnumName<TEnum>(string? code = null, string? message = null)
        where TEnum : struct, global::System.Enum
    {
        if (ShouldSkip)
        {
            return this;
        }

        var failed = Value switch
        {
            null => false,
            string text => !ValidationChecks.IsEnumName<TEnum>(text),
            _ => true
        };

        return Check("EnumName", failed, "'" + _propertyName + "' must be a valid enum name.", code, message);
    }

    /// <summary>
    /// Requires an integer value to map to a defined enum member value of <typeparamref name="TEnum" />. Null values pass.
    /// </summary>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> EnumValue<TEnum>(string? code = null, string? message = null)
        where TEnum : struct, global::System.Enum
    {
        if (ShouldSkip)
        {
            return this;
        }

        return Check("EnumValue", !ValidationChecks.IsEnumValue<TEnum, TProperty>(Value), "'" + _propertyName + "' must be a valid enum value.", code, message);
    }

    /// <summary>
    /// Requires the property value to equal <paramref name="value" />.
    /// </summary>
    /// <param name="value">The expected value.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public PropertyValidation<T, TProperty> Equal(TProperty value, string? code = null, string? message = null)
    {
        if (ShouldSkip)
        {
            return this;
        }

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
        if (ShouldSkip)
        {
            return this;
        }

        return Check("NotEqual", EqualityComparer<TProperty>.Default.Equals(Value, value), "'" + _propertyName + "' must not be equal to " + value + ".", code, message);
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
        if (ShouldSkip)
        {
            return this;
        }

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
        if (ShouldSkip)
        {
            return this;
        }

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
        if (ShouldSkip)
        {
            return this;
        }

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
        if (ShouldSkip)
        {
            return this;
        }

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
        if (ShouldSkip)
        {
            return this;
        }

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
        if (ShouldSkip)
        {
            return this;
        }

        var context = _context;
        var property = _property;
        return AddAsync("SatisfiesAsync", "'" + _propertyName + "' did not satisfy the custom validation predicate.", async cancellationToken => await predicate(context.Instance, property(context.Instance), cancellationToken).ConfigureAwait(false), code, message);
    }

    internal TProperty Value => _property(_context.Instance);

    internal ValidationContext<T> Context => _context;

    internal string PropertyName => _propertyName;

    internal bool ShouldSkip => _context.ShouldStopValidation ||
        (_state.HasFailure && EffectiveFailureBehavior == ValiantFailureBehavior.FailFast);

    internal PropertyValidation<T, TProperty> Check(string defaultCode, bool failed, string defaultMessage, string? code, string? message)
    {
        if (ShouldSkip)
        {
            return this;
        }

        if (failed)
        {
            _context.AddFailure(_propertyName, code ?? defaultCode, message ?? defaultMessage);
            _state.HasFailure = true;
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
        var state = _state;
        var errorCode = code ?? defaultCode;
        var errorMessage = message ?? defaultMessage;
        var failureBehavior = EffectiveFailureBehavior;
        context.AddAsyncValidation(async cancellationToken =>
        {
            if (context.ShouldStopValidation ||
                (state.HasFailure && failureBehavior == ValiantFailureBehavior.FailFast))
            {
                return;
            }

            if (!await predicate(cancellationToken).ConfigureAwait(false))
            {
                context.AddFailure(propertyName, errorCode, errorMessage);
                state.HasFailure = true;
            }
        });

        return this;
    }

    private ValiantFailureBehavior EffectiveFailureBehavior => _failureBehavior ?? _context.FailureBehavior;

    private static void AddValidatorFailures(
        ValidationContext<T> context,
        string propertyName,
        ValidationResult result,
        PropertyValidationState state)
    {
        if (result.IsValid)
        {
            return;
        }

        state.HasFailure = true;
        foreach (var failure in result.Errors)
        {
            context.AddFailure(CombinePropertyPath(propertyName, failure.PropertyName), failure.ErrorCode, failure.ErrorMessage);
            if (context.ShouldStopValidation)
            {
                break;
            }
        }
    }

    private static string CombinePropertyPath(string propertyName, string childPropertyName)
    {
        if (string.IsNullOrEmpty(childPropertyName))
        {
            return propertyName;
        }

        return childPropertyName.StartsWith("[", StringComparison.Ordinal)
            ? propertyName + childPropertyName
            : propertyName + "." + childPropertyName;
    }

    private sealed class PropertyValidationState
    {
        public bool HasFailure { get; set; }
    }
}
