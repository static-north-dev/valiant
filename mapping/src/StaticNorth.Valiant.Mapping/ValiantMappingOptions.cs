namespace StaticNorth.Valiant.Mapping;

/// <summary>
/// Configures behavior captured when a generated Valiant mapper is constructed.
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
}
