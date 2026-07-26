namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Represents the result of serializing a value to a query string.
/// </summary>
public sealed class QuerySerializationResult
{
    internal QuerySerializationResult(string queryString, IReadOnlyList<QueryError> errors)
    {
        QueryString = queryString;
        Errors = errors;
    }

    /// <summary>
    /// Gets whether serialization completed without validation or configuration errors.
    /// </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>
    /// Gets the serialized query string when serialization succeeds.
    /// </summary>
    public string QueryString { get; }

    /// <summary>
    /// Gets structured validation and configuration errors.
    /// </summary>
    public IReadOnlyList<QueryError> Errors { get; }
}
