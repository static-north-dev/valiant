namespace StaticNorth.Valiant.Endpoints.AspNetCore;

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
            SupportedArrayFormats = CloneFormats(options.SupportedArrayFormats),
            SupportedObjectFormats = CloneFormats(options.SupportedObjectFormats),
            EnableJsonQueryValues = options.EnableJsonQueryValues,
            UseStrictBinding = options.UseStrictBinding
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
            SupportedArrayFormats = CloneFormats(supportedArrayFormats ?? options.SupportedArrayFormats),
            SupportedObjectFormats = CloneFormats(supportedObjectFormats ?? options.SupportedObjectFormats),
            EnableJsonQueryValues = enableJsonQueryValues ?? options.EnableJsonQueryValues,
            UseStrictBinding = options.UseStrictBinding
        };
    }

    private static T[] CloneFormats<T>(T[]? formats) => formats is null ? [] : [.. formats];
}
