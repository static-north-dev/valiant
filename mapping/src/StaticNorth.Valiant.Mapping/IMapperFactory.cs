namespace StaticNorth.Valiant.Mapping;

/// <summary>
/// Creates Valiant mapper instances by source and target type.
/// </summary>
public interface IMapperFactory
{
    /// <summary>
    /// Creates the mapper for the specified source and target type pair.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <returns>The mapper for the specified source and target type pair.</returns>
    IMapper<TSource, TTarget> CreateMapper<TSource, TTarget>();

    /// <summary>
    /// Creates the mapper for the specified source and target type pair.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <param name="targetType">The target type.</param>
    /// <returns>The mapper for the specified source and target type pair.</returns>
    object CreateMapper(Type sourceType, Type targetType);

    /// <summary>
    /// Attempts to create the mapper for the specified source and target type pair.
    /// </summary>
    /// <param name="mapper">The created mapper, when one is registered.</param>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <returns><see langword="true" /> when a mapper is registered; otherwise, <see langword="false" />.</returns>
    bool TryCreateMapper<TSource, TTarget>(out IMapper<TSource, TTarget>? mapper);

    /// <summary>
    /// Attempts to create the mapper for the specified source and target type pair.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="mapper">The created mapper, when one is registered.</param>
    /// <returns><see langword="true" /> when a mapper is registered; otherwise, <see langword="false" />.</returns>
    bool TryCreateMapper(Type sourceType, Type targetType, out object? mapper);
}
