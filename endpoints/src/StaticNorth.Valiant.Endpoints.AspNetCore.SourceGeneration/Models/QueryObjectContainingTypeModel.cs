namespace StaticNorth.Valiant.Endpoints.AspNetCore.SourceGeneration;

internal readonly struct QueryObjectContainingTypeModel(
    string modifier,
    string keyword,
    string name) : IEquatable<QueryObjectContainingTypeModel>
{
    public string Modifier { get; } = modifier;

    public string Keyword { get; } = keyword;

    public string Name { get; } = name;

    public bool Equals(QueryObjectContainingTypeModel other)
    {
        return string.Equals(Modifier, other.Modifier, StringComparison.Ordinal) &&
            string.Equals(Keyword, other.Keyword, StringComparison.Ordinal) &&
            string.Equals(Name, other.Name, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is QueryObjectContainingTypeModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(Modifier);
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Keyword);
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Name);
            return hashCode;
        }
    }
}
