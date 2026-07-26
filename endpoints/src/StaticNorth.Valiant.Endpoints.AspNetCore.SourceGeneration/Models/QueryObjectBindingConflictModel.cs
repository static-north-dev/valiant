using StaticNorth.Valiant.SourceGeneration;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.SourceGeneration;

internal readonly struct QueryObjectBindingConflictModel(
    string type,
    ValiantDiagnosticLocation location) : IEquatable<QueryObjectBindingConflictModel>
{
    public string Type { get; } = type;

    public ValiantDiagnosticLocation Location { get; } = location;

    public bool Equals(QueryObjectBindingConflictModel other)
    {
        return string.Equals(Type, other.Type, StringComparison.Ordinal) &&
            Location.Equals(other.Location);
    }

    public override bool Equals(object? obj)
    {
        return obj is QueryObjectBindingConflictModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(Type) * 397) ^ Location.GetHashCode();
        }
    }
}
