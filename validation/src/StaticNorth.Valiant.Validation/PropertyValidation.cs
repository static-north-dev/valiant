namespace Valiant.Validation;

/// <summary>
/// Fluent property validation builder used by Valiant validators.
/// </summary>
/// <typeparam name="T">The model type being validated.</typeparam>
/// <typeparam name="TProperty">The property value type being validated.</typeparam>
public readonly struct PropertyValidation<T, TProperty>
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
    /// <returns>A property validation builder configured to stop after the first failed check.</returns>
    public PropertyValidation<T, TProperty> FailFast()
    {
        return new PropertyValidation<T, TProperty>(_context, _property, _propertyName, ValiantFailureBehavior.FailFast, _state);
    }

    /// <summary>
    /// Runs every check in this property chain even when an earlier check failed.
    /// </summary>
    /// <returns>A property validation builder configured to run every check.</returns>
    public PropertyValidation<T, TProperty> ValidateAll()
    {
        return new PropertyValidation<T, TProperty>(_context, _property, _propertyName, ValiantFailureBehavior.ValidateAll, _state);
    }

    /// <summary>
    /// Runs this property chain only when <paramref name="predicate" /> returns true for the model instance.
    /// </summary>
    /// <param name="predicate">The model predicate that controls whether this property chain runs.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> When(Func<T, bool> predicate)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _state.AddCondition(predicate);
        return this;
    }

    /// <summary>
    /// Reuses a validator for the property value. Null property values are skipped; use <see cref="Required" /> when the property must be present.
    /// </summary>
    /// <typeparam name="TValidatedProperty">The property type accepted by the validator.</typeparam>
    /// <param name="validator">The validator for the property value.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> UseValidator<TValidatedProperty>(IPropertyValidator<TValidatedProperty> validator)
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
        var propertyValidation = this;

        context.AddDeferredValidation(
            () =>
            {
                if (context.ShouldStopValidation ||
                    !state.ShouldRun(context.Instance) ||
                    (state.HasFailure && failureBehavior == ValiantFailureBehavior.FailFast))
                {
                    return;
                }

                if (!propertyValidation.TryGetValue(out var rawValue))
                {
                    return;
                }

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
                    !state.ShouldRun(context.Instance) ||
                    (state.HasFailure && failureBehavior == ValiantFailureBehavior.FailFast))
                {
                    return;
                }

                if (!propertyValidation.TryGetValue(out var rawValue))
                {
                    return;
                }

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
    /// <param name="severity">The severity level of the validation failure.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> Required(string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        if (ShouldSkip)
        {
            return this;
        }

        var propertyValidation = this;
        return Check("Required", () =>
        {
            var value = propertyValidation.Value;
            return value is string text
                ? string.IsNullOrWhiteSpace(text)
                : EqualityComparer<TProperty>.Default.Equals(value, default!);
        }, "'" + _propertyName + "' is required.", code, message, severity);
    }

    /// <summary>
    /// Requires a non-empty value. For strings and collections, null and empty values fail; whitespace-only strings pass. For other values, null or default values fail.
    /// </summary>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    /// <param name="severity">The severity level of the validation failure.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> NotEmpty(string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        if (ShouldSkip)
        {
            return this;
        }

        var propertyValidation = this;
        return Check("NotEmpty", () => ValidationChecks.IsEmpty(propertyValidation.Value), "'" + _propertyName + "' must not be empty.", code, message, severity);
    }

    /// <summary>
    /// Requires an empty value. For strings and collections, null and empty values pass; whitespace-only strings fail.
    /// </summary>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    /// <param name="severity">The severity level of the validation failure.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> Empty(string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        if (ShouldSkip)
        {
            return this;
        }

        var propertyValidation = this;
        return Check("Empty", () => !ValidationChecks.IsEmpty(propertyValidation.Value), "'" + _propertyName + "' must be empty.", code, message, severity);
    }

    /// <summary>
    /// Requires a string value to match a defined enum member name of <typeparamref name="TEnum" />. Null values pass.
    /// </summary>
    /// <typeparam name="TEnum">The enum type whose member names are allowed.</typeparam>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    /// <param name="severity">The severity level of the validation failure.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> EnumName<TEnum>(string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
        where TEnum : struct, Enum
    {
        if (ShouldSkip)
        {
            return this;
        }

        var propertyValidation = this;
        return Check("EnumName", () => propertyValidation.Value switch
        {
            null => false,
            string text => !ValidationChecks.IsEnumName<TEnum>(text),
            _ => true
        }, "'" + _propertyName + "' must be a valid enum name.", code, message, severity);
    }

    /// <summary>
    /// Requires an integer value to map to a defined enum member value of <typeparamref name="TEnum" />. Null values pass.
    /// </summary>
    /// <typeparam name="TEnum">The enum type whose values are allowed.</typeparam>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    /// <param name="severity">The severity level of the validation failure.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> EnumValue<TEnum>(string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
        where TEnum : struct, Enum
    {
        if (ShouldSkip)
        {
            return this;
        }

        var propertyValidation = this;
        return Check("EnumValue", () => !ValidationChecks.IsEnumValue<TEnum, TProperty>(propertyValidation.Value), "'" + _propertyName + "' must be a valid enum value.", code, message, severity);
    }

    /// <summary>
    /// Requires the property value to equal <paramref name="value" />.
    /// </summary>
    /// <param name="value">The expected value.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    /// <param name="severity">The severity level of the validation failure.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> Equal(TProperty value, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        if (ShouldSkip)
        {
            return this;
        }

        var propertyValidation = this;
        return Check("Equal", () => !EqualityComparer<TProperty>.Default.Equals(propertyValidation.Value, value), "'" + _propertyName + "' must be equal to " + value + ".", code, message, severity);
    }

    /// <summary>
    /// Requires the property value to not equal <paramref name="value" />.
    /// </summary>
    /// <param name="value">The disallowed value.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    /// <param name="severity">The severity level of the validation failure.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> NotEqual(TProperty value, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        if (ShouldSkip)
        {
            return this;
        }

        var propertyValidation = this;
        return Check("NotEqual", () => EqualityComparer<TProperty>.Default.Equals(propertyValidation.Value, value), "'" + _propertyName + "' must not be equal to " + value + ".", code, message, severity);
    }

    /// <summary>
    /// Requires the property value to satisfy <paramref name="predicate" />.
    /// </summary>
    /// <param name="predicate">The predicate that must return true for the value to be valid.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    /// <param name="severity">The severity level of the validation failure.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> Satisfies(Func<TProperty, bool> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        if (ShouldSkip)
        {
            return this;
        }

        var propertyValidation = this;
        return Check("Satisfies", () => !predicate(propertyValidation.Value), "'" + _propertyName + "' did not satisfy the custom validation predicate.", code, message, severity);
    }

    /// <summary>
    /// Requires the model and property value to satisfy <paramref name="predicate" />.
    /// </summary>
    /// <param name="predicate">The predicate that must return true for the value to be valid.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    /// <param name="severity">The severity level of the validation failure.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> Satisfies(Func<T, TProperty, bool> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        if (ShouldSkip)
        {
            return this;
        }

        var context = _context;
        var propertyValidation = this;
        return Check("Satisfies", () => !predicate(context.Instance, propertyValidation.Value), "'" + _propertyName + "' did not satisfy the custom validation predicate.", code, message, severity);
    }

    /// <summary>
    /// Requires the property value to satisfy an asynchronous <paramref name="predicate" />.
    /// </summary>
    /// <param name="predicate">The asynchronous predicate that must return true for the value to be valid.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    /// <param name="severity">The severity level of the validation failure.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> SatisfiesAsync(Func<TProperty, CancellationToken, ValueTask<bool>> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        if (ShouldSkip)
        {
            return this;
        }

        var context = _context;
        var property = _property;
        return AddAsync("SatisfiesAsync", "'" + _propertyName + "' did not satisfy the custom validation predicate.", cancellationToken => predicate(property(context.Instance), cancellationToken), code, message, severity);
    }

    /// <summary>
    /// Requires the model and property value to satisfy an asynchronous <paramref name="predicate" />.
    /// </summary>
    /// <param name="predicate">The asynchronous predicate that must return true for the value to be valid.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    /// <param name="severity">The severity level of the validation failure.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> SatisfiesAsync(Func<T, TProperty, CancellationToken, ValueTask<bool>> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        if (ShouldSkip)
        {
            return this;
        }

        var context = _context;
        var property = _property;
        return AddAsync("SatisfiesAsync", "'" + _propertyName + "' did not satisfy the custom validation predicate.", cancellationToken => predicate(context.Instance, property(context.Instance), cancellationToken), code, message, severity);
    }

    /// <summary>
    /// Requires the property value to satisfy an asynchronous <paramref name="predicate" />.
    /// </summary>
    /// <param name="predicate">The asynchronous predicate that must return true for the value to be valid.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    /// <param name="severity">The severity level of the validation failure.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> SatisfiesAsync(Func<TProperty, CancellationToken, Task<bool>> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        if (ShouldSkip)
        {
            return this;
        }

        var context = _context;
        var property = _property;
        return AddAsync("SatisfiesAsync", "'" + _propertyName + "' did not satisfy the custom validation predicate.", async cancellationToken => await predicate(property(context.Instance), cancellationToken).ConfigureAwait(false), code, message, severity);
    }

    /// <summary>
    /// Requires the model and property value to satisfy an asynchronous <paramref name="predicate" />.
    /// </summary>
    /// <param name="predicate">The asynchronous predicate that must return true for the value to be valid.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    /// <param name="severity">The severity level of the validation failure.</param>
    /// <returns>The current property validation builder.</returns>
    public PropertyValidation<T, TProperty> SatisfiesAsync(Func<T, TProperty, CancellationToken, Task<bool>> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        if (ShouldSkip)
        {
            return this;
        }

        var context = _context;
        var property = _property;
        return AddAsync("SatisfiesAsync", "'" + _propertyName + "' did not satisfy the custom validation predicate.", async cancellationToken => await predicate(context.Instance, property(context.Instance), cancellationToken).ConfigureAwait(false), code, message, severity);
    }

    internal TProperty Value => TryGetValue(out var value) ? value : default!;

    internal ValidationContext<T> Context => _context;

    internal string PropertyName => _propertyName;

    internal bool ShouldSkip => _context.ShouldStopValidation ||
        (_state.HasFailure && EffectiveFailureBehavior == ValiantFailureBehavior.FailFast);

    internal PropertyValidation<T, TProperty> Check(string defaultCode, Func<bool> failed, string defaultMessage, string? code, string? message, ValiantSeverity severity = ValiantSeverity.Error)
    {
        _ = failed ?? throw new ArgumentNullException(nameof(failed));
        if (ShouldSkip)
        {
            return this;
        }

        var context = _context;
        var propertyName = _propertyName;
        var state = _state;
        var errorCode = code ?? defaultCode;
        var errorMessage = message ?? defaultMessage;
        var failureBehavior = EffectiveFailureBehavior;
        var propertyValidation = this;
        context.AddDeferredValidation(
            () =>
            {
                if (context.ShouldStopValidation ||
                    !state.ShouldRun(context.Instance) ||
                    (state.HasFailure && failureBehavior == ValiantFailureBehavior.FailFast))
                {
                    return;
                }

                if (!propertyValidation.TryGetValue(out _))
                {
                    return;
                }

                if (failed())
                {
                    context.AddFailure(propertyName, errorCode, errorMessage, severity);
                    state.HasFailure = true;
                }
            },
            cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (context.ShouldStopValidation ||
                    !state.ShouldRun(context.Instance) ||
                    (state.HasFailure && failureBehavior == ValiantFailureBehavior.FailFast))
                {
                    return default;
                }

                if (!propertyValidation.TryGetValue(out _))
                {
                    return default;
                }

                if (failed())
                {
                    context.AddFailure(propertyName, errorCode, errorMessage, severity);
                    state.HasFailure = true;
                }

                return default;
            });

        return this;
    }

    internal bool ShouldRun()
    {
        return _state.ShouldRun(_context.Instance);
    }

    internal bool ShouldSkipExecution()
    {
        return ShouldSkip || !ShouldRun();
    }

    internal bool TryGetValue(out TProperty value)
    {
        try
        {
            value = _property(_context.Instance);
            return true;
        }
        catch (NullReferenceException) when (_propertyName.IndexOf('.') >= 0)
        {
            value = default!;
            return false;
        }
    }

    private PropertyValidation<T, TProperty> AddAsync(
        string defaultCode,
        string defaultMessage,
        Func<CancellationToken, ValueTask<bool>> predicate,
        string? code,
        string? message,
        ValiantSeverity severity)
    {
        var context = _context;
        var propertyName = _propertyName;
        var state = _state;
        var errorCode = code ?? defaultCode;
        var errorMessage = message ?? defaultMessage;
        var failureBehavior = EffectiveFailureBehavior;
        var propertyValidation = this;
        context.AddAsyncValidation(async cancellationToken =>
        {
            if (context.ShouldStopValidation ||
                !state.ShouldRun(context.Instance) ||
                (state.HasFailure && failureBehavior == ValiantFailureBehavior.FailFast))
            {
                return;
            }

            if (!propertyValidation.TryGetValue(out _))
            {
                return;
            }

            if (!await predicate(cancellationToken).ConfigureAwait(false))
            {
                context.AddFailure(propertyName, errorCode, errorMessage, severity);
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
        if (result.Failures.Count == 0)
        {
            return;
        }

        state.HasFailure = true;
        foreach (var failure in result.Failures)
        {
            context.AddFailure(CombinePropertyPath(propertyName, failure.PropertyName), failure.ErrorCode, failure.ErrorMessage, failure.Severity);
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
        private Func<T, bool>? _condition;

        public bool HasFailure { get; set; }

        public void AddCondition(Func<T, bool> condition)
        {
            _ = condition ?? throw new ArgumentNullException(nameof(condition));
            if (_condition is null)
            {
                _condition = condition;
                return;
            }

            var previous = _condition;
            _condition = instance => previous(instance) && condition(instance);
        }

        public bool ShouldRun(T instance)
        {
            return _condition?.Invoke(instance) ?? true;
        }
    }
}
