namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Configures which query string formats are accepted by <see cref="QueryStringParser" />.
/// </summary>
public sealed class QueryParsingOptions
{
    /// <summary>
    /// Gets the array formats accepted by the parser. By default only repeated keys are accepted.
    /// </summary>
    public QueryArrayFormat[] SupportedArrayFormats { get; init; } = [QueryArrayFormat.RepeatedKeys];

    /// <summary>
    /// Gets the object formats accepted by the parser. By default only flat top-level query objects are accepted.
    /// </summary>
    public QueryObjectFormat[] SupportedObjectFormats { get; init; } = [QueryObjectFormat.Flat];

    /// <summary>
    /// Gets whether JSON-encoded query values are accepted for complex objects. This is disabled by default.
    /// </summary>
    public bool EnableJsonQueryValues { get; init; }
}
