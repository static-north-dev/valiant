namespace Valiant;

/// <summary>
/// Validation declaration and runtime validation surface used by Valiant validators.
/// </summary>
/// <typeparam name="T">The model type being validated.</typeparam>
public sealed class ValidationContext<T>
{
    private readonly List<ValidationFailure> _failures = [];
    private readonly List<Func<CancellationToken, ValueTask>> _asyncValidations = [];
    private readonly List<ValidationContextDeferredValidation> _deferredValidations = [];
    private readonly string _propertyPathPrefix;
    private ValiantFailureBehavior _failureBehavior;

    /// <summary>
    /// Initializes a validation context for an instance.
    /// </summary>
    /// <param name="instance">The model instance being validated.</param>
    public ValidationContext(T instance)
        : this(instance, string.Empty, ValiantFailureBehavior.ValidateAll)
    {
    }

    internal ValidationContext(T instance, string propertyPathPrefix, ValiantFailureBehavior failureBehavior = ValiantFailureBehavior.ValidateAll)
        : this(instance, propertyPathPrefix, [], [], [], failureBehavior)
    {
    }

    private ValidationContext(
        T instance,
        string propertyPathPrefix,
        List<ValidationFailure> failures,
        List<Func<CancellationToken, ValueTask>> asyncValidations,
        List<ValidationContextDeferredValidation> deferredValidations,
        ValiantFailureBehavior failureBehavior)
    {
        Instance = instance;
        _propertyPathPrefix = propertyPathPrefix ?? throw new ArgumentNullException(nameof(propertyPathPrefix));
        _failures = failures ?? throw new ArgumentNullException(nameof(failures));
        _asyncValidations = asyncValidations ?? throw new ArgumentNullException(nameof(asyncValidations));
        _deferredValidations = deferredValidations ?? throw new ArgumentNullException(nameof(deferredValidations));
        _failureBehavior = failureBehavior;
    }

    /// <summary>
    /// Gets the model instance being validated.
    /// </summary>
    public T Instance { get; }

    internal ValiantFailureBehavior FailureBehavior => _failureBehavior;

    internal bool ShouldStopValidation => _failureBehavior == ValiantFailureBehavior.FailFast && _failures.Count > 0;

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
        return new PropertyValidation<T, TProperty>(this, property, GetPropertyName(propertyExpression));
    }

    /// <summary>
    /// Stops this validator after the first validation failure.
    /// </summary>
    /// <returns>The current validation context.</returns>
    public ValidationContext<T> FailFast()
    {
        _failureBehavior = ValiantFailureBehavior.FailFast;
        return this;
    }

    /// <summary>
    /// Collects all validation failures for this validator.
    /// </summary>
    /// <returns>The current validation context.</returns>
    public ValidationContext<T> ValidateAll()
    {
        _failureBehavior = ValiantFailureBehavior.ValidateAll;
        return this;
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
            _asyncValidations,
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
            () => AddFailures(validator.Validate(Instance).Errors),
            async cancellationToken => AddFailures((await validator.ValidateAsync(Instance, cancellationToken).ConfigureAwait(false)).Errors));
        return this;
    }

    internal int AddFailure(string propertyName, string errorCode, string errorMessage)
    {
        _failures.Add(new ValidationFailure(ApplyPropertyPathPrefix(propertyName), errorCode, errorMessage));
        return _failures.Count - 1;
    }

    internal void AddAsyncValidation(Func<CancellationToken, ValueTask> validation)
    {
        if (ShouldStopValidation)
        {
            return;
        }

        _asyncValidations.Add(validation ?? throw new ArgumentNullException(nameof(validation)));
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
            AddFailure(failure.PropertyName, failure.ErrorCode, failure.ErrorMessage);
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

    /// <summary>
    /// Completes synchronous validation and returns the collected result.
    /// </summary>
    /// <returns>The validation result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when asynchronous validations were declared and <see cref="ToResultAsync" /> should be used instead.</exception>
    public ValidationResult ToResult()
    {
        foreach (var validation in _deferredValidations)
        {
            if (ShouldStopValidation)
            {
                break;
            }

            validation.Validate();
        }

        if (_asyncValidations.Count > 0 && !ShouldStopValidation)
        {
            throw new InvalidOperationException("This validator contains asynchronous validations. Call ValidateAsync instead.");
        }

        return _failures.Count == 0 ? ValidationResult.Valid : new ValidationResult(_failures);
    }

    /// <summary>
    /// Completes validation, including asynchronous checks, and returns the collected result.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for asynchronous validations.</param>
    /// <returns>The validation result.</returns>
    public async ValueTask<ValidationResult> ToResultAsync(CancellationToken cancellationToken = default)
    {
        foreach (var validation in _deferredValidations)
        {
            if (ShouldStopValidation)
            {
                break;
            }

            await validation.ValidateAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var validation in _asyncValidations)
        {
            if (ShouldStopValidation)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await validation(cancellationToken).ConfigureAwait(false);
        }

        return _failures.Count == 0 ? ValidationResult.Valid : new ValidationResult(_failures);
    }

    private static string GetPropertyName(string? propertyExpression)
    {
        if (string.IsNullOrWhiteSpace(propertyExpression))
        {
            return string.Empty;
        }

        var expression = propertyExpression!;
        var lastDot = expression.LastIndexOf('.');
        var name = lastDot >= 0 ? expression.Substring(lastDot + 1) : expression;
        var end = name.IndexOfAny(new[] { ' ', ')', '}' });
        return end >= 0 ? name.Substring(0, end) : name;
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

        return _propertyPathPrefix + propertyName;
    }

}

internal sealed class ValidationContextDeferredValidation(
    Action validate,
    Func<CancellationToken, ValueTask> validateAsync)
{
    public Action Validate { get; } = validate;

    public Func<CancellationToken, ValueTask> ValidateAsync { get; } = validateAsync;
}
