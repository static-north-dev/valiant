using Microsoft.CodeAnalysis;

namespace StaticNorth.Valiant.SourceGeneration;

/// <summary>
/// Stores a diagnostic as value data suitable for an incremental generator pipeline.
/// </summary>
internal sealed class ValiantDiagnosticInfo(
    DiagnosticDescriptor descriptor,
    ValiantDiagnosticLocation location,
    params object?[] messageArgs) : IEquatable<ValiantDiagnosticInfo>
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

    public bool Equals(ValiantDiagnosticInfo? other)
    {
        return other is not null && Descriptor.Equals(other.Descriptor) &&
            Location.Equals(other.Location) && MessageArgs.SequenceEqual(other.MessageArgs);
    }

    public override bool Equals(object? obj) => obj is ValiantDiagnosticInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (Descriptor.GetHashCode() * 397) ^ Location.GetHashCode();
            foreach (var argument in MessageArgs)
            {
                hashCode = (hashCode * 397) ^ (argument?.GetHashCode() ?? 0);
            }

            return hashCode;
        }
    }
}
