namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Configures query parameter parsing and serialization behavior for Valiant endpoint helpers.
/// </summary>
public sealed class QueryParameterOptions
{
    /// <summary>
    /// Gets or sets whether Valiant generates and applies custom query parameter binding.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false" />, which leaves query parameter binding to ASP.NET Core.
    /// Set this directly to <see langword="true" /> in the <c>AddValiantEndpoints</c> setup lambda to opt the current compilation into generated query binding.
    /// Generated direct <c>TryParse</c> methods cannot observe a later runtime change to this option because they do not receive an <c>HttpContext</c>.
    /// Avoid compiling the same endpoint assembly with a recognized <see langword="true" /> assignment and then running it with this option overridden to <see langword="false" />.
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
