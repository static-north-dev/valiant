using Microsoft.CodeAnalysis;

namespace StaticNorth.Valiant.SourceGeneration;

/// <summary>
/// Stores a diagnostic as value data suitable for an incremental generator pipeline.
/// </summary>
internal sealed class ValiantDiagnosticInfo(
    DiagnosticDescriptor descriptor,
    ValiantDiagnosticLocation location,
    params object?[] messageArgs)
{
    public ValiantDiagnosticInfo(
        DiagnosticDescriptor descriptor,
        Location? location,
        params object?[] messageArgs)
        : this(descriptor, ValiantDiagnosticLocation.Create(location), messageArgs)
    {
    }

    public DiagnosticDescriptor Descriptor { get; } = descriptor;

    public ValiantDiagnosticLocation Location { get; } = location;

    public object?[] MessageArgs { get; } = messageArgs.Length == 0 ? [] : [.. messageArgs];

    public Diagnostic Create()
    {
        return Diagnostic.Create(Descriptor, Location.ToLocation(), MessageArgs);
    }

    public string GetMessage()
    {
        return Create().GetMessage();
    }
}
