namespace StaticNorth.Valiant.Mapping;

/// <summary>
/// Configures runtime behavior for generated Valiant mappers.
/// </summary>
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
