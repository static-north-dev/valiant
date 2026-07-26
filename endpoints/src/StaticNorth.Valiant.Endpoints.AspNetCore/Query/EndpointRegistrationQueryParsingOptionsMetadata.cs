namespace StaticNorth.Valiant.Endpoints.AspNetCore;

internal sealed class EndpointRegistrationQueryParsingOptionsMetadata
{
    internal EndpointRegistrationQueryParsingOptionsMetadata(QueryParsingOptions options)
    {
        Options = QueryParsingOptionsSnapshot.Clone(options);
    }

    internal QueryParsingOptions Options { get; }
}
