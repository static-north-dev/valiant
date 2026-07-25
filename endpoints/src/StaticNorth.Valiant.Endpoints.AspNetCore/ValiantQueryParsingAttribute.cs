namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Configures source-generated query parsing for an endpoint.
/// </summary>
/// <remarks>
/// This attribute is ignored when query binding is disabled at source-generation time.
/// A generated direct <c>TryParse</c> method cannot observe a later runtime-only change to <see cref="QueryParameterOptions.EnableBinding" />.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ValiantQueryParsingAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the accepted array formats for full-query binding.
    /// </summary>
    /// <remarks>
    /// Omit this property or set it to <see langword="null" /> to inherit the endpoint-local or global parsing options.
    /// An empty array disables every array format for this endpoint.
    /// This property does not affect direct complex <c>[FromQuery]</c> parameters.
    /// </remarks>
    public QueryArrayFormat[]? SupportedArrayFormats { get; set; }

    /// <summary>
    /// Gets or sets the accepted object formats for full-query binding.
    /// </summary>
    /// <remarks>
    /// Omit this property or set it to <see langword="null" /> to inherit the endpoint-local or global parsing options.
    /// An empty array disables every object format for this endpoint.
    /// This property does not affect direct complex <c>[FromQuery]</c> parameters.
    /// </remarks>
    public QueryObjectFormat[]? SupportedObjectFormats { get; set; }

    /// <summary>
    /// Gets or sets whether generated query parsers accept JSON values.
    /// </summary>
    /// <remarks>
    /// This always controls Valiant-generated direct complex <c>[FromQuery]</c> parsers.
    /// For full-query binding, it overrides endpoint-local or global parsing options only when explicitly specified.
    /// </remarks>
    public JsonQueryValues JsonValues { get; set; } = JsonQueryValues.Enabled;
}
