using System.ComponentModel;

namespace StaticNorth.Valiant.Validation;

/// <summary>
/// Validation declaration and runtime validation surface used by Valiant validators.
/// </summary>
/// <typeparam name="T">The model type being validated.</typeparam>
public sealed class ValidationContext<T>
{
    private readonly List<ValidationFailure> _failures;
    private readonly List<ValidationContextDeferredValidation> _deferredValidations;
    private readonly string _propertyPathPrefix;
    private ValiantFailureBehavior _failureBehavior;
    private ValiantFailureBehavior _executionFailureBehavior;
    private int _failureCountAtBehaviorChange;

    /// <summary>
    /// Initializes a validation context for an instance.
    /// </summary>
    /// <param name="instance">The model instance being validated.</param>
    public ValidationContext(T instance)
        : this(instance, string.Empty)
    {
    }

    internal ValidationContext(T instance, string propertyPathPrefix, ValiantFailureBehavior failureBehavior = ValiantFailureBehavior.ValidateAll)
        : this(instance, propertyPathPrefix, [], [], failureBehavior)
    {
    }

    private ValidationContext(
        T instance,
        string propertyPathPrefix,
        List<ValidationFailure> failures,
        List<ValidationContextDeferredValidation> deferredValidations,
        ValiantFailureBehavior failureBehavior)
    {
        Instance = instance;
        _propertyPathPrefix = propertyPathPrefix ?? throw new ArgumentNullException(nameof(propertyPathPrefix));
        _failures = failures ?? throw new ArgumentNullException(nameof(failures));
        _deferredValidations = deferredValidations ?? throw new ArgumentNullException(nameof(deferredValidations));
        _failureBehavior = failureBehavior;
        _executionFailureBehavior = failureBehavior;
    }

    /// <summary>
    /// Gets the model instance being validated.
    /// </summary>
    public T Instance { get; }

    internal ValiantFailureBehavior FailureBehavior => _failureBehavior;

    internal bool ShouldStopValidation
    {
        get
        {
            if (_executionFailureBehavior != ValiantFailureBehavior.FailFast)
            {
                return false;
            }

            for (var index = _failureCountAtBehaviorChange; index < _failures.Count; index++)
            {
                if (_failures[index].Severity == ValiantSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Starts validation for a model property.
    /// </summary>
    /// <param name="property">The property selector to validate.</param>
    /// <param name="propertyExpression">The source expression used to infer the property name.</param>
    /// <returns>A property validation builder for adding checks.</returns>
    public PropertyValidation<T, TProperty> Property<TProperty>(
        Func<T, TProperty> property,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(property))] string? propertyExpression = null)
    {
        _ = property ?? throw new ArgumentNullException(nameof(property));
        var propertyName = GetPropertyName(propertyExpression);
        return new PropertyValidation<T, TProperty>(
            this,
            property,
            ApplyPropertyPathPrefix(propertyName),
            propertyName.IndexOf('.') >= 0);
    }

    /// <summary>
    /// Stops this validator on the first error from the rules declared after this call.
    /// </summary>
    /// <returns>The current validation context.</returns>
    public ValidationContext<T> FailFast()
    {
        SetFailureBehavior(ValiantFailureBehavior.FailFast);
        return this;
    }

    /// <summary>
    /// Collects all failures from the rules declared after this call.
    /// </summary>
    /// <returns>The current validation context.</returns>
    public ValidationContext<T> ValidateAll()
    {
        SetFailureBehavior(ValiantFailureBehavior.ValidateAll);
        return this;
    }

    private void SetFailureBehavior(ValiantFailureBehavior behavior)
    {
        _failureBehavior = behavior;
        if (_deferredValidations.Count == 0)
        {
            _executionFailureBehavior = behavior;
            return;
        }

        // Apply changes between queued rules, leaving earlier checks under their original policy.
        AddSyncValidation(ApplyBehavior);

        void ApplyBehavior()
        {
            _executionFailureBehavior = behavior;
            _failureCountAtBehaviorChange = _failures.Count;
        }
    }

    /// <summary>
    /// Creates a validation context for a specific item inside a collection property.
    /// </summary>
    /// <param name="item">The collection item being validated.</param>
    /// <param name="collection">The collection property selector that owns the item.</param>
    /// <param name="index">The item index inside the collection.</param>
    /// <param name="collectionExpression">The source expression used to infer the collection property name.</param>
    /// <returns>A validation context scoped to the collection item.</returns>
    public ValidationContext<TItem> Item<TItem, TCollection>(
        TItem item,
        Func<T, TCollection> collection,
        int index,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(collection))] string? collectionExpression = null)
        where TCollection : IEnumerable<TItem>?
    {
        _ = collection ?? throw new ArgumentNullException(nameof(collection));
        return new ValidationContext<TItem>(
            item,
            CreateChildPropertyPathPrefix(GetPropertyName(collectionExpression), index),
            _failures,
            _deferredValidations,
            _failureBehavior);
    }

    /// <summary>
    /// Reuses another validator for the same model type.
    /// </summary>
    /// <param name="validator">The validator to execute as part of this validation context.</param>
    /// <returns>The current validation context.</returns>
    public ValidationContext<T> UseValidator(IValidator<T> validator)
    {
        _ = validator ?? throw new ArgumentNullException(nameof(validator));
        if (ShouldStopValidation)
        {
            return this;
        }

        AddDeferredValidation(
            () => AddFailures(validator.Validate(Instance).Failures),
            async cancellationToken => AddFailures((await validator.ValidateAsync(Instance, cancellationToken).ConfigureAwait(false)).Failures));
        return this;
    }

    internal int AddFailure(string propertyName, string errorCode, string errorMessage, ValiantSeverity severity = ValiantSeverity.Error)
    {
        _failures.Add(new ValidationFailure(ApplyPropertyPathPrefix(propertyName), errorCode, errorMessage, severity));
        return _failures.Count - 1;
    }

    private void AddSyncValidation(Action validation)
    {
        if (!ShouldStopValidation)
        {
            _deferredValidations.Add(new ValidationContextDeferredValidation(validation, null));
        }
    }

    internal void AddSyncValidation(IDeferredValidationCheck validation)
    {
        if (!ShouldStopValidation)
        {
            _deferredValidations.Add(new ValidationContextDeferredValidation(validation, null));
        }
    }

    internal void AddAsyncValidation(Func<CancellationToken, ValueTask> validation)
    {
        if (ShouldStopValidation)
        {
            return;
        }

        _deferredValidations.Add(new ValidationContextDeferredValidation(
            null, validation ?? throw new ArgumentNullException(nameof(validation))));
    }

    internal void AddDeferredValidation(Action validate, Func<CancellationToken, ValueTask> validateAsync)
    {
        if (ShouldStopValidation)
        {
            return;
        }

        _deferredValidations.Add(new ValidationContextDeferredValidation(
            validate ?? throw new ArgumentNullException(nameof(validate)),
            validateAsync ?? throw new ArgumentNullException(nameof(validateAsync))));
    }

    internal void AddFailures(IEnumerable<ValidationFailure> failures)
    {
        foreach (var failure in failures)
        {
            AddFailure(failure.PropertyName, failure.ErrorCode, failure.ErrorMessage, failure.Severity);
            if (ShouldStopValidation)
            {
                break;
            }
        }
    }

    internal string CreateChildPropertyPathPrefix(string propertyName, int index)
    {
        var prefix = ApplyPropertyPathPrefix(propertyName);
        return string.IsNullOrEmpty(prefix)
            ? "[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]."
            : prefix + "[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].";
    }

    internal string CreateChildPropertyPathPrefix(string propertyName)
    {
        var prefix = ApplyPropertyPathPrefix(propertyName);
        return string.IsNullOrEmpty(prefix)
            ? string.Empty
            : prefix + ".";
    }

    /// <summary>
    /// Completes synchronous validation and returns the collected result.
    /// </summary>
    /// <returns>The validation result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when asynchronous validations were declared and <see cref="ToResultAsync" /> should be used instead.</exception>
    public ValidationResult ToResult()
    {
        Execute();
        return BuildResult();
    }

    /// <summary>
    /// Executes synchronous validation and returns its validity.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool ToStatus()
    {
        Execute();
        return GetStatus();
    }

    private void Execute()
    {
        foreach (var validation in _deferredValidations)
        {
            if (ShouldStopValidation)
            {
                break;
            }

            if (validation.RequiresAsync)
            {
                throw new InvalidOperationException("This validator contains asynchronous validations. Call ValidateAsync instead.");
            }

            validation.Execute();
        }
    }

    /// <summary>
    /// Completes validation, including asynchronous checks, and returns the collected result.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for asynchronous validations.</param>
    /// <returns>The validation result.</returns>
    public ValueTask<ValidationResult> ToResultAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(static context => context.BuildResult(), cancellationToken);
    }

    /// <summary>
    /// Executes validation, including asynchronous checks, and returns its validity.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for asynchronous validations.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ValueTask<bool> ToStatusAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(static context => context.GetStatus(), cancellationToken);
    }

    private async ValueTask<TResult> ExecuteAsync<TResult>(
        Func<ValidationContext<T>, TResult> finish,
        CancellationToken cancellationToken)
    {
        foreach (var validation in _deferredValidations)
        {
            if (ShouldStopValidation)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (validation.ValidateAsync is { } validateAsync)
            {
                await validateAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                validation.Execute();
            }
        }

        return finish(this);
    }

    private ValidationResult BuildResult()
    {
        return _failures.Count == 0 ? ValidationResult.Valid : new ValidationResult(_failures);
    }

    private bool GetStatus()
    {
        foreach (var failure in _failures)
        {
            if (ValidationResult.IsError(failure.Severity))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetPropertyName(string? propertyExpression)
    {
        if (string.IsNullOrWhiteSpace(propertyExpression))
        {
            return string.Empty;
        }

        var expression = propertyExpression!;
        var arrow = expression.IndexOf("=>", StringComparison.Ordinal);
        var start = 0;
        var end = expression.Length;
        if (arrow >= 0)
        {
            start = arrow + 2;
            while (start < end && char.IsWhiteSpace(expression[start]))
            {
                start++;
            }

            while (end > start && char.IsWhiteSpace(expression[end - 1]))
            {
                end--;
            }
        }

        var firstDot = expression.IndexOf('.', start, end - start);
        if (firstDot >= 0)
        {
            start = firstDot + 1;
        }

        var nameEnd = start;
        while (nameEnd < end && expression[nameEnd] is not (' ' or ')' or '}'))
        {
            nameEnd++;
        }

        return expression.Substring(start, nameEnd - start).Replace("!", string.Empty);
    }

    private string ApplyPropertyPathPrefix(string propertyName)
    {
        if (string.IsNullOrEmpty(_propertyPathPrefix))
        {
            return propertyName;
        }

        if (string.IsNullOrEmpty(propertyName))
        {
            return _propertyPathPrefix.TrimEnd('.');
        }

        if (propertyName.StartsWith(_propertyPathPrefix, StringComparison.Ordinal))
        {
            return propertyName;
        }

        return _propertyPathPrefix + propertyName;
    }

}
