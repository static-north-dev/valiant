namespace Valiant;

/// <summary>
/// Validation declaration and runtime validation surface used by Valiant validators.
/// </summary>
/// <typeparam name="T">The model type being validated.</typeparam>
public sealed class ValidationContext<T>
{
    private readonly List<ValidationFailure> _failures = [];
    private readonly List<Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask>> _asyncValidations = [];
    private readonly List<IValidator<T>> _reusedValidators = [];

    /// <summary>
    /// Initializes a validation context for an instance.
    /// </summary>
    /// <param name="instance">The model instance being validated.</param>
    public ValidationContext(T instance)
    {
        Instance = instance;
    }

    /// <summary>
    /// Gets the model instance being validated.
    /// </summary>
    public T Instance { get; }

    /// <summary>
    /// Starts validation for a model property.
    /// </summary>
    /// <param name="property">The property selector to validate.</param>
    /// <param name="propertyExpression">The source expression used to infer the property name.</param>
    /// <returns>A property validation builder for adding checks.</returns>
    public PropertyValidation<T, TProperty> Property<TProperty>(
        Func<T, TProperty> property,
        [global::System.Runtime.CompilerServices.CallerArgumentExpression(nameof(property))] string? propertyExpression = null)
    {
        _ = property ?? throw new ArgumentNullException(nameof(property));
        return new PropertyValidation<T, TProperty>(this, property, GetPropertyName(propertyExpression));
    }

    /// <summary>
    /// Reuses another validator for the same model type.
    /// </summary>
    /// <param name="validator">The validator to execute as part of this validation context.</param>
    /// <returns>The current validation context.</returns>
    public ValidationContext<T> UseValidator(IValidator<T> validator)
    {
        _ = validator ?? throw new ArgumentNullException(nameof(validator));
        _reusedValidators.Add(validator);
        return this;
    }

    internal int AddFailure(string propertyName, string errorCode, string errorMessage)
    {
        _failures.Add(new ValidationFailure(propertyName, errorCode, errorMessage));
        return _failures.Count - 1;
    }

    internal void AddAsyncValidation(Func<global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask> validation)
    {
        _asyncValidations.Add(validation ?? throw new ArgumentNullException(nameof(validation)));
    }

    internal void AddFailures(IEnumerable<ValidationFailure> failures)
    {
        _failures.AddRange(failures);
    }

    /// <summary>
    /// Completes synchronous validation and returns the collected result.
    /// </summary>
    /// <returns>The validation result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when asynchronous validations were declared and <see cref="ToResultAsync" /> should be used instead.</exception>
    public ValidationResult ToResult()
    {
        foreach (var validator in _reusedValidators)
        {
            AddFailures(validator.Validate(Instance).Errors);
        }

        if (_asyncValidations.Count > 0)
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
    public async global::System.Threading.Tasks.ValueTask<ValidationResult> ToResultAsync(global::System.Threading.CancellationToken cancellationToken = default)
    {
        foreach (var validator in _reusedValidators)
        {
            AddFailures((await validator.ValidateAsync(Instance, cancellationToken).ConfigureAwait(false)).Errors);
        }

        foreach (var validation in _asyncValidations)
        {
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
}
