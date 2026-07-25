namespace Valiant.Endpoints.AspNetCore;

/// <summary>
/// Describes a source-generated endpoint before it is mapped.
/// </summary>
public sealed class EndpointRegistration
{
    internal EndpointRegistration(
        Type endpointType,
        EndpointGroupRegistration? group,
        int order,
        EndpointQueryParameterRegistration queryParameters,
        EndpointConfigurationOptions configuration)
    {
        EndpointType = endpointType;
        Group = group;
        Order = order;
        QueryParameters = queryParameters;
        Configuration = configuration;
    }

    /// <summary>
    /// Gets the endpoint type.
    /// </summary>
    public Type EndpointType { get; }

    /// <summary>
    /// Gets the containing generated group, or <see langword="null" /> for a standalone endpoint.
    /// </summary>
    public EndpointGroupRegistration? Group { get; }

    /// <summary>
    /// Gets the source-generated endpoint mapping order.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Gets whether the endpoint is assigned to a generated group.
    /// </summary>
    public bool IsGrouped => Group is not null;

    /// <summary>
    /// Gets the query parameter registration snapshot.
    /// </summary>
    public EndpointQueryParameterRegistration QueryParameters { get; }

    internal EndpointConfigurationOptions Configuration { get; }
}
