namespace StaticNorth.Valiant.Endpoints.AspNetCore.SourceGeneration;

internal readonly struct EndpointModel(
    string type,
    EndpointKind kind,
    string? groupType,
    int order,
    EndpointQueryParsingOverridesModel queryParsingOverrides = default) : IEquatable<EndpointModel>
{
    public string Type { get; } = type;

    public EndpointKind Kind { get; } = kind;

    public string? GroupType { get; } = groupType;

    public int Order { get; } = order;

    public EndpointQueryParsingOverridesModel QueryParsingOverrides { get; } = queryParsingOverrides;

    public bool Equals(EndpointModel other)
    {
        return string.Equals(Type, other.Type, StringComparison.Ordinal) &&
            Kind == other.Kind &&
            string.Equals(GroupType, other.GroupType, StringComparison.Ordinal) &&
            Order == other.Order &&
            QueryParsingOverrides.Equals(other.QueryParsingOverrides);
    }

    public override bool Equals(object? obj)
    {
        return obj is EndpointModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(Type);
            hashCode = (hashCode * 397) ^ Kind.GetHashCode();
            hashCode = (hashCode * 397) ^ (GroupType is null ? 0 : StringComparer.Ordinal.GetHashCode(GroupType));
            hashCode = (hashCode * 397) ^ Order;
            hashCode = (hashCode * 397) ^ QueryParsingOverrides.GetHashCode();
            return hashCode;
        }
    }
}
