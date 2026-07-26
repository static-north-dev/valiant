using System.Diagnostics.CodeAnalysis;

namespace StaticNorth.Valiant.Mapping;

/// <summary>
/// Base type for source-generated mapper definitions.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TTarget">The target type.</typeparam>
public abstract class MapperDefinition<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget> : IMapperDefinition<TSource, TTarget>
{
    /// <summary>
    /// Configures explicit mapping rules.
    /// </summary>
    /// <param name="context">The mapping declaration context.</param>
    public virtual void Configure(MappingContext<TSource, TTarget> context) { }
}
