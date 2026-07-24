namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Configures one generated endpoint during registration.
/// </summary>
public sealed class EndpointConfigurationOptions
{
    internal EndpointConfigurationOptions(QueryParsingOptions parsing)
    {
        QueryParameters = new EndpointQueryParameterConfigurationOptions(parsing);
    }

    /// <summary>
    /// Gets query parameter configuration for the endpoint.
    /// </summary>
    public EndpointQueryParameterConfigurationOptions QueryParameters { get; }

    internal bool IsConditional { get; private set; }

    internal void MarkConditional() => IsConditional = true;
}

/// <summary>
/// Configures query parameter parsing for one generated endpoint.
/// </summary>
public sealed class EndpointQueryParameterConfigurationOptions
{
    internal EndpointQueryParameterConfigurationOptions(QueryParsingOptions parsing)
    {
        Parsing = QueryParsingOptionsSnapshot.Clone(parsing);
    }

    /// <summary>
    /// Gets or sets the registration-level query parsing options.
    /// </summary>
    public QueryParsingOptions Parsing
    {
        get;
        set => field = value is null
            ? throw new ArgumentNullException(nameof(value))
            : QueryParsingOptionsSnapshot.Clone(value);
    }
}

internal static class QueryParsingOptionsSnapshot
{
    internal static QueryParsingOptions Clone(QueryParsingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new QueryParsingOptions
        {
            SupportedArrayFormats = options.SupportedArrayFormats is null
                ? null!
                : [.. options.SupportedArrayFormats],
            SupportedObjectFormats = options.SupportedObjectFormats is null
                ? null!
                : [.. options.SupportedObjectFormats],
            EnableJsonQueryValues = options.EnableJsonQueryValues
        };
    }

    internal static QueryParsingOptions ApplyOverrides(
        QueryParsingOptions options,
        QueryArrayFormat[]? supportedArrayFormats,
        QueryObjectFormat[]? supportedObjectFormats,
        bool? enableJsonQueryValues)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new QueryParsingOptions
        {
            SupportedArrayFormats = supportedArrayFormats is null
                ? Clone(options).SupportedArrayFormats
                : [.. supportedArrayFormats],
            SupportedObjectFormats = supportedObjectFormats is null
                ? Clone(options).SupportedObjectFormats
                : [.. supportedObjectFormats],
            EnableJsonQueryValues = enableJsonQueryValues ?? options.EnableJsonQueryValues
        };
    }
}
