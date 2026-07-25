namespace Valiant.SourceGeneration;

/// <summary>
/// Identifies a source and target type pair using stable generated type names.
/// </summary>
internal readonly struct ValiantTypePair(
    string sourceType,
    string targetType) : IEquatable<ValiantTypePair>
{
    public string SourceType { get; } = sourceType;

    public string TargetType { get; } = targetType;

    public bool Equals(ValiantTypePair other)
    {
        return string.Equals(SourceType, other.SourceType, StringComparison.Ordinal) &&
            string.Equals(TargetType, other.TargetType, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is ValiantTypePair other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(SourceType) * 397) ^
                StringComparer.Ordinal.GetHashCode(TargetType);
        }
    }
}
