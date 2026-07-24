namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Defines the query string formats that can represent object values.
/// </summary>
public enum QueryObjectFormat
{
    /// <summary>
    /// Object values are represented by top-level query keys, such as <c>?brand=tesla&amp;year=2025</c>.
    /// </summary>
    Flat,

    /// <summary>
    /// Object values are represented by dotted paths, such as <c>?filter.brand=tesla</c>.
    /// </summary>
    DotNotation,

    /// <summary>
    /// Object values are represented by bracket paths, such as <c>?filter[brand]=tesla</c>.
    /// </summary>
    DeepObject
}
