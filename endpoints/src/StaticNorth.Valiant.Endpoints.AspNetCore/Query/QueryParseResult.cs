namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Represents the result of parsing a query string.
/// </summary>
public sealed class QueryParseResult
{
    internal QueryParseResult(object? value, IReadOnlyList<QueryError> errors)
    {
        Value = value;
        Errors = errors;
    }

    /// <summary>
    /// Gets whether parsing completed without validation or configuration errors.
    /// </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>
    /// Gets the parsed value when parsing succeeds.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Gets structured validation and configuration errors.
    /// </summary>
    public IReadOnlyList<QueryError> Errors { get; }
}

/// <summary>
/// Represents the result of parsing a query string.
/// </summary>
/// <typeparam name="T">The parsed value type.</typeparam>
public sealed class QueryParseResult<T>
{
    internal QueryParseResult(T? value, IReadOnlyList<QueryError> errors)
    {
        Value = value;
        Errors = errors;
    }

    /// <summary>
    /// Gets whether parsing completed without validation or configuration errors.
    /// </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>
    /// Gets the parsed value when parsing succeeds.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets structured validation and configuration errors.
    /// </summary>
    public IReadOnlyList<QueryError> Errors { get; }
}
