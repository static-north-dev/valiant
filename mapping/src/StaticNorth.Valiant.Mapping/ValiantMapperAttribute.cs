namespace Valiant.Mapping;

/// <summary>
/// Marks a partial type as a Valiant mapper.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ValiantMapperAttribute : Attribute
{
    /// <summary>
    /// Initializes a mapper marker attribute. The mapper pair is inferred from MapperDefinition&lt;TSource, TTarget&gt; or IMapperDefinition&lt;TSource, TTarget&gt;.
    /// </summary>
    public ValiantMapperAttribute()
    {
    }
}
