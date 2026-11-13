using System.ComponentModel;

namespace StaticNorth.Valiant.Mapping.Infrastructure;

/// <summary>
/// Resolves a source-generated mapper without constructing generic types at runtime.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ValiantMapperRegistration<TSource, TTarget>() :
    ValiantMapperRegistration(typeof(TSource), typeof(TTarget))
{
    internal override object? Resolve(IServiceProvider services) => services.GetService(typeof(IMapper<TSource, TTarget>));

    internal override bool TryMap(IServiceProvider services, object source, out object? destination)
    {
        if (Resolve(services) is IMapper<TSource, TTarget> mapper)
        {
            destination = mapper.Map((TSource)source);
            return true;
        }

        destination = null;
        return false;
    }
}
