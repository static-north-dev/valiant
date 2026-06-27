namespace Valiant;

/// <summary>
/// Compile-time fluent rule builder consumed by the Valiant source generator.
/// </summary>
public readonly struct RuleBuilder<T, TProperty>
{
    public RuleBuilder<T, TProperty> NotNull() => this;

    public RuleBuilder<T, TProperty> Null() => this;

    public RuleBuilder<T, TProperty> NotEmpty() => this;

    public RuleBuilder<T, TProperty> Empty() => this;

    public RuleBuilder<T, TProperty> Length(int min, int max) => this;

    public RuleBuilder<T, TProperty> MinimumLength(int min) => this;

    public RuleBuilder<T, TProperty> MaximumLength(int max) => this;

    public RuleBuilder<T, TProperty> GreaterThan(TProperty value) => this;

    public RuleBuilder<T, TProperty> GreaterThanOrEqualTo(TProperty value) => this;

    public RuleBuilder<T, TProperty> LessThan(TProperty value) => this;

    public RuleBuilder<T, TProperty> LessThanOrEqualTo(TProperty value) => this;

    public RuleBuilder<T, TProperty> Equal(TProperty value) => this;

    public RuleBuilder<T, TProperty> NotEqual(TProperty value) => this;

    public RuleBuilder<T, TProperty> InclusiveBetween(TProperty from, TProperty to) => this;

    public RuleBuilder<T, TProperty> ExclusiveBetween(TProperty from, TProperty to) => this;

    public RuleBuilder<T, TProperty> Must(Func<TProperty, bool> predicate)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    public RuleBuilder<T, TProperty> Must(Func<T, TProperty, bool> predicate)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    public RuleBuilder<T, TProperty> MustAsync(Func<TProperty, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<bool>> predicate)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    public RuleBuilder<T, TProperty> MustAsync(Func<T, TProperty, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<bool>> predicate)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    public RuleBuilder<T, TProperty> MustAsync(Func<TProperty, global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task<bool>> predicate)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    public RuleBuilder<T, TProperty> MustAsync(Func<T, TProperty, global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task<bool>> predicate)
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    public RuleBuilder<T, TProperty> WithMessage(string message)
    {
        _ = message ?? throw new ArgumentNullException(nameof(message));
        return this;
    }
}
