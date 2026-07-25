namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Stores query parsing options that apply to a specific mapped endpoint.
/// </summary>
internal sealed class EndpointQueryParsingOptionsMetadata
{
    internal EndpointQueryParsingOptionsMetadata(QueryParsingOptions options)
    {
        Options = options;
    }

    /// <summary>
    /// Gets the endpoint-specific query parsing options.
    /// </summary>
    public QueryParsingOptions Options { get; }
}
