namespace Valiant.Mapping;

/// <summary>
/// Legacy base type for runtime mappers.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TTarget">The target type.</typeparam>
public abstract class Mapper<TSource, TTarget> : IMapper<TSource, TTarget>
{
    /// <inheritdoc />
    public abstract TTarget Map(TSource source);
}
