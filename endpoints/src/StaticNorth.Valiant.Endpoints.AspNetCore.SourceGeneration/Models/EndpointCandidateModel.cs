using System.Collections.Immutable;
using Valiant.SourceGeneration;

namespace Valiant.Endpoints.AspNetCore.SourceGeneration;

internal sealed class EndpointCandidateModel(
    EndpointModel? endpoint,
    ImmutableArray<QueryObjectBindingModel> queryObjectBindings,
    ImmutableArray<ValiantDiagnosticInfo> diagnostics)
{
    public EndpointCandidateModel(EndpointModel? endpoint, ImmutableArray<ValiantDiagnosticInfo> diagnostics)
        : this(endpoint, ImmutableArray<QueryObjectBindingModel>.Empty, diagnostics)
    {
    }

    public EndpointModel? Endpoint { get; } = endpoint;

    public ImmutableArray<QueryObjectBindingModel> QueryObjectBindings { get; } = queryObjectBindings;

    public ImmutableArray<ValiantDiagnosticInfo> Diagnostics { get; } = diagnostics;
}
