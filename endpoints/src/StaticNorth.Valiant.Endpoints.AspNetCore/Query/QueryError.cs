namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Represents a structured query parsing or serialization error.
/// </summary>
/// <param name="path">The query path associated with the error.</param>
/// <param name="code">A stable machine-readable error code.</param>
/// <param name="message">A human-readable error message.</param>
/// <param name="kind">Whether the error came from invalid input or disabled configuration.</param>
public sealed class QueryError(string path, string code, string message, QueryErrorKind kind)
{
    /// <summary>
    /// Gets the query path associated with the error.
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Gets a stable machine-readable error code.
    /// </summary>
    public string Code { get; } = code;

    /// <summary>
    /// Gets a human-readable error message.
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// Gets whether the error came from invalid input or disabled configuration.
    /// </summary>
    public QueryErrorKind Kind { get; } = kind;
}
