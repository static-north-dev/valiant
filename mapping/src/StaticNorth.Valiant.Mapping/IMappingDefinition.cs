namespace Valiant.Mapping;

/// <summary>
/// Defines the mapping contract for a source-generated mapper.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TTarget">The target type.</typeparam>
public interface IMappingDefinition<TSource, TTarget> : IMapperDefinition<TSource, TTarget>
{
}
