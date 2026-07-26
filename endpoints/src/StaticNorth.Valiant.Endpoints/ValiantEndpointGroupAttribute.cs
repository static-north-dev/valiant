namespace StaticNorth.Valiant.Endpoints;

/// <summary>
/// Marks an endpoint group declaration for source-generated Valiant endpoint registration.
/// </summary>
/// <remarks>
/// Endpoint groups referenced by <see cref="ValiantEndpointAttribute" /> are mapped automatically.
/// Apply this attribute when a group should be mapped without referenced endpoints, or when group mapping order matters.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ValiantEndpointGroupAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the relative source-generated mapping order. Groups with the same order are sorted by type name.
    /// </summary>
    public int Order { get; set; }
}
