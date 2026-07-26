namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Describes query parameter binding and parsing before an endpoint is mapped.
/// </summary>
public sealed class EndpointQueryParameterRegistration
{
    private readonly QueryParsingOptions _preMappingParsingOptions;

    internal EndpointQueryParameterRegistration(
        bool isBindingGenerated,
        bool isRuntimeBindingEnabled,
        QueryParsingOptions preMappingParsingOptions)
    {
        IsBindingGenerated = isBindingGenerated;
        IsRuntimeBindingEnabled = isRuntimeBindingEnabled;
        _preMappingParsingOptions = QueryParsingOptionsSnapshot.Clone(preMappingParsingOptions);
    }

    /// <summary>
    /// Gets whether query binding support was emitted for the endpoint compilation.
    /// </summary>
    public bool IsBindingGenerated { get; }

    /// <summary>
    /// Gets whether runtime query binding filters are enabled for this registration.
    /// </summary>
    /// <remarks>
    /// Generated direct <c>TryParse</c> members are controlled by <see cref="IsBindingGenerated" /> and cannot be disabled at runtime.
    /// </remarks>
    public bool IsRuntimeBindingEnabled { get; }

    /// <summary>
    /// Gets a defensive copy of the parsing options known before <c>IEndpoint.Map</c> executes.
    /// </summary>
    /// <remarks>
    /// Endpoint-authored <c>WithQueryParsingOptions</c> metadata is not available until the endpoint is mapped.
    /// </remarks>
    public QueryParsingOptions PreMappingParsingOptions =>
        QueryParsingOptionsSnapshot.Clone(_preMappingParsingOptions);
}
