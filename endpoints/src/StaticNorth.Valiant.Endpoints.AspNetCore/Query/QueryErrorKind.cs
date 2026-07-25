namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Defines the kind of error produced while parsing or serializing a query string.
/// </summary>
public enum QueryErrorKind
{
    /// <summary>
    /// The query string or value could not be parsed for the requested type.
    /// </summary>
    Validation,

    /// <summary>
    /// The query string or requested output uses a format disabled by configuration.
    /// </summary>
    Configuration
}
