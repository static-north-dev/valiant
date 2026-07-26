namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Defines the query string formats that can represent array values.
/// </summary>
public enum QueryArrayFormat
{
    /// <summary>
    /// Array values are represented by repeating the same query key, such as <c>?ids=1&amp;ids=2</c>.
    /// </summary>
    RepeatedKeys,

    /// <summary>
    /// Array values are represented by a comma-separated value, such as <c>?ids=1,2</c>.
    /// </summary>
    CommaSeparated,

    /// <summary>
    /// Array values are represented by bracket keys, such as <c>?ids[]=1&amp;ids[]=2</c>.
    /// </summary>
    Brackets,

    /// <summary>
    /// Array values are represented by indexed bracket keys, such as <c>?ids[0]=1&amp;ids[1]=2</c>.
    /// </summary>
    IndexedBrackets
}
