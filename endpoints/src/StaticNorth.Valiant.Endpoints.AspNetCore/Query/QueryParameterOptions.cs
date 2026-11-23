namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Configures query parameter parsing and serialization behavior for Valiant endpoint helpers.
/// </summary>
public sealed class QueryParameterOptions
{
    /// <summary>
    /// Gets or sets the compatibility opt-in for custom query parameter binding.
    /// </summary>
    /// <remarks>
    /// Use <see cref="ValiantGenerateQueryBindingAttribute" /> on the endpoint assembly to opt into generation.
    /// A literal <see langword="true" /> assignment in the <c>AddValiantEndpoints</c> setup lambda also opts into generation for compatibility.
    /// For generated registrations, the compile-time opt-in controls all forms of generated query binding.
    /// Older registration entry points without compile-time binding metadata continue to read this option.
    /// </remarks>
    public bool EnableBinding { get; set; }

    /// <summary>
    /// Gets or sets configurable query parsing behavior.
    /// </summary>
    public QueryParsingOptions Parsing
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = new();
}
