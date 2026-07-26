namespace StaticNorth.Valiant.Validation;

/// <summary>
/// Complex-object property validation extensions.
/// </summary>
public static class ChildPropertyValidationExtensions
{
    /// <summary>
    /// Runs validation declarations for a non-null child property value.
    /// </summary>
    /// <param name="property">The child property validation builder.</param>
    /// <param name="configure">The child validation declarations.</param>
    /// <returns>The property validation builder.</returns>
    public static PropertyValidation<T, TChild?> Children<T, TChild>(this PropertyValidation<T, TChild?> property, Action<ValidationContext<TChild>> configure)
        where TChild : class
    {
        _ = configure ?? throw new ArgumentNullException(nameof(configure));
        return ChildrenCore(
            property,
            static (childContext, _, configure) => configure(childContext),
            configure);
    }

    /// <summary>
    /// Runs validation declarations for a non-null child property value with access to the whole model instance.
    /// </summary>
    /// <param name="property">The child property validation builder.</param>
    /// <param name="configure">The child validation declarations.</param>
    /// <returns>The property validation builder.</returns>
    public static PropertyValidation<T, TChild?> Children<T, TChild>(this PropertyValidation<T, TChild?> property, Action<ValidationContext<TChild>, T> configure)
        where TChild : class
    {
        _ = configure ?? throw new ArgumentNullException(nameof(configure));
        return ChildrenCore<T, TChild, Action<ValidationContext<TChild>, T>>(
            property,
            static (childContext, instance, configure) => configure(childContext, instance),
            configure);
    }

    private static PropertyValidation<T, TChild?> ChildrenCore<T, TChild, TState>(
        PropertyValidation<T, TChild?> property,
        Action<ValidationContext<TChild>, T, TState> configure,
        TState state)
        where TChild : class
    {
        _ = configure ?? throw new ArgumentNullException(nameof(configure));
        if (property.ShouldSkip)
        {
            return property;
        }

        property.Context.AddDeferredValidation(
            () =>
            {
                if (property.ShouldSkipExecution() ||
                    !property.TryGetValue(out var child) ||
                    child is null)
                {
                    return;
                }

                var childContext = new ValidationContext<TChild>(child, property.Context.CreateChildPropertyPathPrefix(property.PropertyName));
                configure(childContext, property.Context.Instance, state);
                property.Context.AddFailures(childContext.ToResult().Failures);
            },
            async cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (property.ShouldSkipExecution() ||
                    !property.TryGetValue(out var child) ||
                    child is null)
                {
                    return;
                }

                var childContext = new ValidationContext<TChild>(child, property.Context.CreateChildPropertyPathPrefix(property.PropertyName));
                configure(childContext, property.Context.Instance, state);
                property.Context.AddFailures((await childContext.ToResultAsync(cancellationToken).ConfigureAwait(false)).Failures);
            });

        return property;
    }
}
