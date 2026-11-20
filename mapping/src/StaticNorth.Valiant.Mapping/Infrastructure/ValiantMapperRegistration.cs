using System.ComponentModel;

namespace StaticNorth.Valiant.Mapping.Infrastructure;

/// <summary>
/// Describes a source-generated mapper registration for dynamic lookup.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class ValiantMapperRegistration
{
    internal ValiantMapperRegistration(Type sourceType, Type targetType)
    {
        SourceType = sourceType;
        TargetType = targetType;
    }

    internal Type SourceType { get; }

    internal Type TargetType { get; }

    internal abstract object? Resolve(IServiceProvider services);

    internal abstract bool TryMap(IServiceProvider services, object source, out object? destination);
}
