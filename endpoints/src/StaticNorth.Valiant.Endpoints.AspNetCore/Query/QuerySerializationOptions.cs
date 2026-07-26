namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Configures which query string formats are emitted by <see cref="QueryStringSerializer" />.
/// </summary>
public sealed class QuerySerializationOptions
{
    /// <summary>
    /// Gets the selected array format to emit.
    /// </summary>
    public QueryArrayFormat ArrayFormat { get; init; } = QueryArrayFormat.RepeatedKeys;

    /// <summary>
    /// Gets the selected object format to emit.
    /// </summary>
    public QueryObjectFormat ObjectFormat { get; init; } = QueryObjectFormat.Flat;

    /// <summary>
    /// Gets whether complex object values can be emitted as JSON query values.
    /// </summary>
    public bool EnableJsonQueryValues { get; init; }
}
