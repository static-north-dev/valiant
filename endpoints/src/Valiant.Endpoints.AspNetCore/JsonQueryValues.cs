namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Controls whether a Valiant-generated direct query parser accepts JSON values.
/// </summary>
public enum JsonQueryValues
{
    /// <summary>
    /// Rejects JSON query values.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Accepts JSON query values.
    /// </summary>
    Enabled = 1
}
