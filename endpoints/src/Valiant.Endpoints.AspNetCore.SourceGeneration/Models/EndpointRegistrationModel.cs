using System.Collections.Immutable;

namespace Valiant.Endpoints.AspNetCore.SourceGeneration;

internal readonly struct EndpointRegistrationModel(
    ImmutableArray<EndpointModel> endpoints,
    bool queryBindingGenerated) : IEquatable<EndpointRegistrationModel>
{
    public ImmutableArray<EndpointModel> Endpoints { get; } = endpoints;

    public bool QueryBindingGenerated { get; } = queryBindingGenerated;

    public bool IsEmpty => Endpoints.IsDefaultOrEmpty;

    public bool Equals(EndpointRegistrationModel other)
    {
        return QueryBindingGenerated == other.QueryBindingGenerated &&
            Endpoints.SequenceEqual(other.Endpoints, EndpointModelComparer.Instance);
    }

    public override bool Equals(object? obj)
    {
        return obj is EndpointRegistrationModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = 17;
            hashCode = (hashCode * 397) ^ QueryBindingGenerated.GetHashCode();
            foreach (var endpoint in Endpoints)
            {
                hashCode = (hashCode * 397) ^ endpoint.GetHashCode();
            }

            return hashCode;
        }
    }
}
