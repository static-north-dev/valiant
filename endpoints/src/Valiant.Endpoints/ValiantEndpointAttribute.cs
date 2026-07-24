namespace Valiant.Endpoints;

/// <summary>
/// Marks an endpoint declaration for source-generated Valiant endpoint registration.
/// </summary>
/// <remarks>
/// Apply it to an endpoint without a group type to map the endpoint at the root builder.
/// Apply it to an endpoint with a group type to map the endpoint inside that generated group.
/// Use <see cref="ValiantEndpointGroupAttribute" /> to mark endpoint groups directly.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ValiantEndpointAttribute : Attribute
{
    /// <summary>
    /// Initializes a new endpoint marker.
    /// </summary>
    public ValiantEndpointAttribute()
    {
    }

    /// <summary>
    /// Initializes a new endpoint marker for an endpoint that should be mapped inside an endpoint group.
    /// </summary>
    /// <param name="groupType">The endpoint group type that should contain the marked endpoint.</param>
    public ValiantEndpointAttribute(Type groupType)
    {
        GroupType = groupType ?? throw new ArgumentNullException(nameof(groupType));
    }

    /// <summary>
    /// Gets the endpoint group type that should contain the marked endpoint, when supplied.
    /// </summary>
    public Type? GroupType { get; }

    /// <summary>
    /// Gets or sets the relative source-generated mapping order. Endpoints with the same order are sorted by type name.
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// Marks an endpoint declaration for source-generated Valiant endpoint registration inside an endpoint group.
/// </summary>
/// <typeparam name="TGroup">The endpoint group type that should contain the marked endpoint.</typeparam>
/// <remarks>
/// This is the generic equivalent of <see cref="ValiantEndpointAttribute(Type)" />.
/// Use <see cref="ValiantEndpointGroupAttribute" /> to mark endpoint groups directly.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ValiantEndpointAttribute<TGroup> : Attribute
{
    /// <summary>
    /// Gets or sets the relative source-generated mapping order. Endpoints with the same order are sorted by type name.
    /// </summary>
    public int Order { get; set; }
}
