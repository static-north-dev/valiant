using System.ComponentModel;

namespace StaticNorth.Valiant.Mapping.Infrastructure;

/// <summary>
/// Resolves mapper pairs registered by every participating assembly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ValiantMapperLookup : IMapper, IMapperFactory
{
    private const int DictionaryLookupThreshold = 8;

    private readonly IServiceProvider _services;
    private readonly ValiantMapperRegistration[] _registrations;
    private readonly Dictionary<(Type Source, Type Target), ValiantMapperRegistration>? _index;

    public ValiantMapperLookup(IServiceProvider services, IEnumerable<ValiantMapperRegistration> registrations)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _ = registrations ?? throw new ArgumentNullException(nameof(registrations));
        _registrations = registrations as ValiantMapperRegistration[] ?? registrations.ToArray();
        if (_registrations.Length > DictionaryLookupThreshold)
        {
            _index = new Dictionary<(Type, Type), ValiantMapperRegistration>(_registrations.Length);
            foreach (var registration in _registrations)
            {
                _index[(registration.SourceType, registration.TargetType)] = registration;
            }
        }
    }

    /// <inheritdoc />
    public TDestination Map<TDestination>(object source) =>
        TryMap<TDestination>(source, out var destination)
            ? destination!
            : throw CreateMissingMapperException(source.GetType(), typeof(TDestination));

    /// <inheritdoc />
    public object Map(object source, Type destinationType) =>
        TryMap(source, destinationType, out var destination)
            ? destination!
            : throw CreateMissingMapperException(source.GetType(), destinationType);

    /// <inheritdoc />
    public bool TryMap<TDestination>(object source, out TDestination? destination)
    {
        if (TryMap(source, typeof(TDestination), out var result))
        {
            destination = (TDestination?)result;
            return true;
        }

        destination = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryMap(object source, Type destinationType, out object? destination)
    {
        _ = source ?? throw new ArgumentNullException(nameof(source));
        _ = destinationType ?? throw new ArgumentNullException(nameof(destinationType));
        var registration = Find(source.GetType(), destinationType);
        if (registration is not null)
        {
            return registration.TryMap(_services, source, out destination);
        }

        destination = null;
        return false;
    }

    /// <inheritdoc />
    public IMapper<TSource, TTarget> CreateMapper<TSource, TTarget>() =>
        TryCreateMapper<TSource, TTarget>(out var mapper)
            ? mapper!
            : throw CreateMissingMapperException(typeof(TSource), typeof(TTarget));

    /// <inheritdoc />
    public object CreateMapper(Type sourceType, Type targetType) =>
        TryCreateMapper(sourceType, targetType, out var mapper)
            ? mapper!
            : throw CreateMissingMapperException(sourceType, targetType);

    /// <inheritdoc />
    public bool TryCreateMapper<TSource, TTarget>(out IMapper<TSource, TTarget>? mapper)
    {
        mapper = Find(typeof(TSource), typeof(TTarget))?.Resolve(_services) as IMapper<TSource, TTarget>;
        return mapper is not null;
    }

    /// <inheritdoc />
    public bool TryCreateMapper(Type sourceType, Type targetType, out object? mapper)
    {
        _ = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        _ = targetType ?? throw new ArgumentNullException(nameof(targetType));
        mapper = Find(sourceType, targetType)?.Resolve(_services);
        return mapper is not null;
    }

    private ValiantMapperRegistration? Find(Type sourceType, Type targetType)
    {
        if (_index is not null)
        {
            return _index.TryGetValue((sourceType, targetType), out var registration) ? registration : null;
        }

        foreach (var registration in _registrations)
        {
            if (registration.SourceType == sourceType && registration.TargetType == targetType)
            {
                return registration;
            }
        }

        return null;
    }

    private static InvalidOperationException CreateMissingMapperException(Type sourceType, Type targetType) =>
        new("No Valiant mapper is registered for source type '" + sourceType.FullName + "' and target type '" +
            targetType.FullName + "'. Register mappers with AddValiantMappers.");
}
