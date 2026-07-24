namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Describes a source-generated endpoint group before it is mapped.
/// </summary>
public sealed class EndpointGroupRegistration
{
    internal EndpointGroupRegistration(Type groupType, int order)
    {
        GroupType = groupType;
        Order = order;
    }

    /// <summary>
    /// Gets the endpoint group type.
    /// </summary>
    public Type GroupType { get; }

    /// <summary>
    /// Gets the source-generated mapping order.
    /// </summary>
    public int Order { get; }
}
