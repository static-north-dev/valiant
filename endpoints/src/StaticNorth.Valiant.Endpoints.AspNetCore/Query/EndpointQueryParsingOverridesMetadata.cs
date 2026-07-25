namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Stores source-generated partial query parsing overrides for an endpoint.
/// </summary>
internal sealed class EndpointQueryParsingOverridesMetadata
{
    internal EndpointQueryParsingOverridesMetadata(
        QueryArrayFormat[]? supportedArrayFormats,
        QueryObjectFormat[]? supportedObjectFormats,
        bool? enableJsonQueryValues)
    {
        SupportedArrayFormats = supportedArrayFormats is null ? null : [.. supportedArrayFormats];
        SupportedObjectFormats = supportedObjectFormats is null ? null : [.. supportedObjectFormats];
        EnableJsonQueryValues = enableJsonQueryValues;
    }

    private QueryArrayFormat[]? SupportedArrayFormats { get; }

    private QueryObjectFormat[]? SupportedObjectFormats { get; }

    private bool? EnableJsonQueryValues { get; }

    internal QueryParsingOptions ApplyTo(QueryParsingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new QueryParsingOptions
        {
            SupportedArrayFormats = Clone(SupportedArrayFormats ?? options.SupportedArrayFormats),
            SupportedObjectFormats = Clone(SupportedObjectFormats ?? options.SupportedObjectFormats),
            EnableJsonQueryValues = EnableJsonQueryValues ?? options.EnableJsonQueryValues
        };
    }

    private static T[] Clone<T>(T[]? values)
    {
        return values is null ? null! : [.. values];
    }
}
