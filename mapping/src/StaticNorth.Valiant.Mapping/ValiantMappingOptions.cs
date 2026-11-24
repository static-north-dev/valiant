namespace StaticNorth.Valiant.Mapping;

/// <summary>
/// Configures Valiant mapper registration and behavior.
/// </summary>
/// <remarks>
/// Configure these options during application startup. Existing mapper instances retain the values
/// captured by their constructor and do not observe later mutations.
/// </remarks>
public sealed class ValiantMappingOptions
{
    /// <summary>
    /// Gets or sets which mapping backend generated mappers use.
    /// </summary>
    public ValiantMappingMode Mode { get; set; } = ValiantMappingMode.Auto;

    /// <summary>
    /// Gets or sets how generated mappers handle null source values.
    /// </summary>
    public ValiantNullHandling NullHandling { get; set; } = ValiantNullHandling.MapNulls;

    /// <summary>
    /// Gets or sets whether generated registration uses scoped lifetimes for
    /// <see cref="IMapper"/> and <see cref="IMapperFactory"/>. The default is singleton.
    /// </summary>
    /// <remarks>
    /// Set this on the first generated registration call when the lookup must resolve
    /// mappers with scoped dependencies.
    /// </remarks>
    public bool UseScopedMapperLookup { get; set; }
}
