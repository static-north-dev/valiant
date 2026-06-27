namespace Valiant;

/// <summary>
/// Compile-time rule declaration surface consumed by the Valiant source generator.
/// </summary>
/// <typeparam name="T">The model type being validated.</typeparam>
public sealed class ValidationContext<T>
{
    /// <summary>
    /// Gets a compile-time placeholder for the model instance being validated.
    /// </summary>
    public T Instance => default!;

    /// <summary>
    /// Starts a rule for a model property.
    /// </summary>
    public RuleBuilder<T, TProperty> RuleFor<TProperty>(Func<T, TProperty> property)
    {
        _ = property ?? throw new ArgumentNullException(nameof(property));
        return default;
    }
}
