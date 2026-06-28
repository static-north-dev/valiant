namespace Valiant.Validation;

/// <summary>
/// Collection-specific property validation extensions.
/// </summary>
public static class CollectionPropertyValidationExtensions
{
    /// <summary>
    /// Requires every item in the list to satisfy <paramref name="predicate" />. Null and empty lists pass.
    /// </summary>
    /// <param name="property">The list property validation builder.</param>
    /// <param name="predicate">The predicate that every item must satisfy.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, List<TItem>> All<T, TItem>(this PropertyValidation<T, List<TItem>> property, Func<TItem, bool> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        return All<T, List<TItem>, TItem>(property, predicate, code, message, severity);
    }

    /// <summary>
    /// Requires every item in the array to satisfy <paramref name="predicate" />. Null and empty arrays pass.
    /// </summary>
    /// <param name="property">The array property validation builder.</param>
    /// <param name="predicate">The predicate that every item must satisfy.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TItem[]?> All<T, TItem>(this PropertyValidation<T, TItem[]?> property, Func<TItem, bool> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        return All<T, TItem[]?, TItem>(property, predicate, code, message, severity);
    }

    /// <summary>
    /// Requires at least one item in the list to satisfy <paramref name="predicate" />. Null and empty lists fail.
    /// </summary>
    /// <param name="property">The list property validation builder.</param>
    /// <param name="predicate">The predicate that at least one item must satisfy.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, List<TItem>> Any<T, TItem>(this PropertyValidation<T, List<TItem>> property, Func<TItem, bool> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        return Any<T, List<TItem>, TItem>(property, predicate, code, message, severity);
    }

    /// <summary>
    /// Requires at least one item in the array to satisfy <paramref name="predicate" />. Null and empty arrays fail.
    /// </summary>
    /// <param name="property">The array property validation builder.</param>
    /// <param name="predicate">The predicate that at least one item must satisfy.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TItem[]?> Any<T, TItem>(this PropertyValidation<T, TItem[]?> property, Func<TItem, bool> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        return Any<T, TItem[]?, TItem>(property, predicate, code, message, severity);
    }

    /// <summary>
    /// Requires no item in the list to satisfy <paramref name="predicate" />. Null and empty lists pass.
    /// </summary>
    /// <param name="property">The list property validation builder.</param>
    /// <param name="predicate">The predicate that no item may satisfy.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, List<TItem>> None<T, TItem>(this PropertyValidation<T, List<TItem>> property, Func<TItem, bool> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        return None<T, List<TItem>, TItem>(property, predicate, code, message, severity);
    }

    /// <summary>
    /// Requires no item in the array to satisfy <paramref name="predicate" />. Null and empty arrays pass.
    /// </summary>
    /// <param name="property">The array property validation builder.</param>
    /// <param name="predicate">The predicate that no item may satisfy.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TItem[]?> None<T, TItem>(this PropertyValidation<T, TItem[]?> property, Func<TItem, bool> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
    {
        return None<T, TItem[]?, TItem>(property, predicate, code, message, severity);
    }

    /// <summary>
    /// Runs validation declarations for every item in a list.
    /// </summary>
    /// <param name="property">The list property validation builder.</param>
    /// <param name="configure">The item validation declarations.</param>
    public static PropertyValidation<T, List<TItem>> ForEach<T, TItem>(this PropertyValidation<T, List<TItem>> property, Action<ValidationContext<TItem>> configure)
    {
        return ForEach<T, List<TItem>, TItem>(property, configure);
    }

    /// <summary>
    /// Runs validation declarations for every item in an array.
    /// </summary>
    /// <param name="property">The array property validation builder.</param>
    /// <param name="configure">The item validation declarations.</param>
    public static PropertyValidation<T, TItem[]?> ForEach<T, TItem>(this PropertyValidation<T, TItem[]?> property, Action<ValidationContext<TItem>> configure)
    {
        return ForEach<T, TItem[]?, TItem>(property, configure);
    }

    /// <summary>
    /// Runs validation declarations for every item in a list with access to the full list.
    /// </summary>
    /// <param name="property">The list property validation builder.</param>
    /// <param name="configure">The item validation declarations.</param>
    public static PropertyValidation<T, List<TItem>> ForEach<T, TItem>(this PropertyValidation<T, List<TItem>> property, Action<ValidationContext<TItem>, List<TItem>> configure)
    {
        return ForEach<T, List<TItem>, TItem>(property, configure);
    }

    /// <summary>
    /// Runs validation declarations for every item in an array with access to the full array.
    /// </summary>
    /// <param name="property">The array property validation builder.</param>
    /// <param name="configure">The item validation declarations.</param>
    public static PropertyValidation<T, TItem[]?> ForEach<T, TItem>(this PropertyValidation<T, TItem[]?> property, Action<ValidationContext<TItem>, TItem[]?> configure)
    {
        return ForEach<T, TItem[]?, TItem>(property, configure);
    }

    /// <summary>
    /// Requires a collection or dictionary to have exactly <paramref name="count" /> item(s). Null values pass.
    /// </summary>
    /// <param name="property">The collection property validation builder.</param>
    /// <param name="count">The exact allowed count.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TCollection> Count<T, TCollection>(this PropertyValidation<T, TCollection> property, int count, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
        where TCollection : System.Collections.ICollection?
    {
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("Count", !ValidationChecks.HasCount(property.Value, count), "'" + property.PropertyName + "' must contain exactly " + count + " item(s).", code, message, severity);
    }

    /// <summary>
    /// Requires every item in the collection to satisfy <paramref name="predicate" />. Null and empty collections pass.
    /// </summary>
    /// <param name="property">The collection property validation builder.</param>
    /// <param name="predicate">The predicate that every item must satisfy.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TCollection> All<T, TCollection, TItem>(this PropertyValidation<T, TCollection> property, Func<TItem, bool> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
        where TCollection : IEnumerable<TItem>?
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("All", !ValidationChecks.All(property.Value, predicate), "'" + property.PropertyName + "' must contain only matching item(s).", code, message, severity);
    }

    /// <summary>
    /// Requires at least one item in the collection to satisfy <paramref name="predicate" />. Null and empty collections fail.
    /// </summary>
    /// <param name="property">The collection property validation builder.</param>
    /// <param name="predicate">The predicate that at least one item must satisfy.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TCollection> Any<T, TCollection, TItem>(this PropertyValidation<T, TCollection> property, Func<TItem, bool> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
        where TCollection : IEnumerable<TItem>?
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("Any", !ValidationChecks.Any(property.Value, predicate), "'" + property.PropertyName + "' must contain at least one matching item.", code, message, severity);
    }

    /// <summary>
    /// Requires no item in the collection to satisfy <paramref name="predicate" />. Null and empty collections pass.
    /// </summary>
    /// <param name="property">The collection property validation builder.</param>
    /// <param name="predicate">The predicate that no item may satisfy.</param>
    /// <param name="code">Optional error code used when this check fails.</param>
    /// <param name="message">Optional error message used when this check fails.</param>
    public static PropertyValidation<T, TCollection> None<T, TCollection, TItem>(this PropertyValidation<T, TCollection> property, Func<TItem, bool> predicate, string? code = null, string? message = null, ValiantSeverity severity = ValiantSeverity.Error)
        where TCollection : IEnumerable<TItem>?
    {
        _ = predicate ?? throw new ArgumentNullException(nameof(predicate));
        if (property.ShouldSkip)
        {
            return property;
        }

        return property.Check("None", !ValidationChecks.None(property.Value, predicate), "'" + property.PropertyName + "' must not contain matching item(s).", code, message, severity);
    }

    /// <summary>
    /// Runs validation declarations for every item in a collection.
    /// </summary>
    /// <param name="property">The collection property validation builder.</param>
    /// <param name="configure">The item validation declarations.</param>
    public static PropertyValidation<T, TCollection> ForEach<T, TCollection, TItem>(this PropertyValidation<T, TCollection> property, Action<ValidationContext<TItem>> configure)
        where TCollection : IEnumerable<TItem>?
    {
        _ = configure ?? throw new ArgumentNullException(nameof(configure));
        return ForEachCore<T, TCollection, TItem, Action<ValidationContext<TItem>>>(
            property,
            static (itemContext, _, configure) => configure(itemContext),
            configure);
    }

    /// <summary>
    /// Runs validation declarations for every item in a collection with access to the full collection.
    /// </summary>
    /// <param name="property">The collection property validation builder.</param>
    /// <param name="configure">The item validation declarations.</param>
    public static PropertyValidation<T, TCollection> ForEach<T, TCollection, TItem>(this PropertyValidation<T, TCollection> property, Action<ValidationContext<TItem>, TCollection> configure)
        where TCollection : IEnumerable<TItem>?
    {
        _ = configure ?? throw new ArgumentNullException(nameof(configure));
        return ForEachCore<T, TCollection, TItem, Action<ValidationContext<TItem>, TCollection>>(
            property,
            static (itemContext, collection, configure) => configure(itemContext, collection),
            configure);
    }

    private static PropertyValidation<T, TCollection> ForEachCore<T, TCollection, TItem, TState>(
        PropertyValidation<T, TCollection> property,
        Action<ValidationContext<TItem>, TCollection, TState> configure,
        TState state)
        where TCollection : IEnumerable<TItem>?
    {
        _ = configure ?? throw new ArgumentNullException(nameof(configure));
        if (property.ShouldSkip)
        {
            return property;
        }

        var collection = property.Value;
        if (collection is null)
        {
            return property;
        }

        var index = 0;
        foreach (var item in collection)
        {
            var itemContext = new ValidationContext<TItem>(item, property.Context.CreateChildPropertyPathPrefix(property.PropertyName, index));
            configure(itemContext, collection, state);
            property.Context.AddFailures(itemContext.ToResult().Errors);
            index++;
        }

        return property;
    }
}
