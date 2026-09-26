using System.Collections.Immutable;
using StaticNorth.Valiant.SourceGeneration;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.SourceGeneration;

internal sealed class EndpointCandidateModel(
    EndpointModel? endpoint,
    ImmutableArray<QueryObjectBindingModel> queryObjectBindings,
    ImmutableArray<ValiantDiagnosticInfo> diagnostics) : IEquatable<EndpointCandidateModel>
{
    public EndpointCandidateModel(EndpointModel? endpoint, ImmutableArray<ValiantDiagnosticInfo> diagnostics)
        : this(endpoint, ImmutableArray<QueryObjectBindingModel>.Empty, diagnostics)
    {
    }

    public EndpointModel? Endpoint { get; } = endpoint;

    public ImmutableArray<QueryObjectBindingModel> QueryObjectBindings { get; } = queryObjectBindings;

    public ImmutableArray<ValiantDiagnosticInfo> Diagnostics { get; } = diagnostics;

    public bool Equals(EndpointCandidateModel? other)
    {
        return other is not null && Nullable.Equals(Endpoint, other.Endpoint) &&
            QueryObjectBindings.SequenceEqual(other.QueryObjectBindings) &&
            Diagnostics.SequenceEqual(other.Diagnostics);
    }

    public override bool Equals(object? obj) => obj is EndpointCandidateModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Endpoint?.GetHashCode() ?? 0;
            foreach (var binding in QueryObjectBindings)
            {
                hashCode = (hashCode * 397) ^ binding.GetHashCode();
            }

            foreach (var diagnostic in Diagnostics)
            {
                hashCode = (hashCode * 397) ^ diagnostic.GetHashCode();
            }

            return hashCode;
        }
    }
}
