namespace StaticNorth.Valiant.Mapping;

/// <summary>
/// Maps source instances to requested target types.
/// </summary>
public interface IMapper
{
    /// <summary>
    /// Maps the specified source instance to <typeparamref name="TDestination" />.
    /// </summary>
    /// <param name="source">The source instance.</param>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <returns>The mapped destination instance.</returns>
    TDestination Map<TDestination>(object source);

    /// <summary>
    /// Maps the specified source instance to the requested destination type.
    /// </summary>
    /// <param name="source">The source instance.</param>
    /// <param name="destinationType">The destination type.</param>
    /// <returns>The mapped destination instance.</returns>
    object Map(object source, Type destinationType);

    /// <summary>
    /// Attempts to map the specified source instance to <typeparamref name="TDestination" />.
    /// </summary>
    /// <param name="source">The source instance.</param>
    /// <param name="destination">The mapped destination instance, when a mapper is registered.</param>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <returns><see langword="true" /> when a mapper is registered; otherwise, <see langword="false" />.</returns>
    bool TryMap<TDestination>(object source, out TDestination? destination);

    /// <summary>
    /// Attempts to map the specified source instance to the requested destination type.
    /// </summary>
    /// <param name="source">The source instance.</param>
    /// <param name="destinationType">The destination type.</param>
    /// <param name="destination">The mapped destination instance, when a mapper is registered.</param>
    /// <returns><see langword="true" /> when a mapper is registered; otherwise, <see langword="false" />.</returns>
    bool TryMap(object source, Type destinationType, out object? destination);
}

/// <summary>
/// Maps an instance of <typeparamref name="TSource" /> to <typeparamref name="TTarget" />.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TTarget">The target type.</typeparam>
public interface IMapper<in TSource, out TTarget>
{
    /// <summary>
    /// Maps the specified source instance to a target instance.
    /// </summary>
    /// <param name="source">The source instance.</param>
    /// <returns>The mapped target instance.</returns>
    TTarget Map(TSource source);
}
