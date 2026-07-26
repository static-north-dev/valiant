using System.Collections.Immutable;
using StaticNorth.Valiant.SourceGeneration;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.SourceGeneration;

internal readonly struct QueryObjectBindingModel(
    QueryObjectBindingKind kind,
    string hintName,
    string? namespaceName,
    string modifier,
    string keyword,
    string name,
    string type,
    bool isValueType,
    bool enableJsonQueryValues,
    ValiantDiagnosticLocation policyLocation,
    ImmutableArray<QueryObjectContainingTypeModel> containingTypes) : IEquatable<QueryObjectBindingModel>
{
    public QueryObjectBindingKind Kind { get; } = kind;

    public string HintName { get; } = hintName;

    public string? NamespaceName { get; } = namespaceName;

    public string Modifier { get; } = modifier;

    public string Keyword { get; } = keyword;

    public string Name { get; } = name;

    public string Type { get; } = type;

    public bool IsValueType { get; } = isValueType;

    public bool EnableJsonQueryValues { get; } = enableJsonQueryValues;

    public ValiantDiagnosticLocation PolicyLocation { get; } = policyLocation;

    public ImmutableArray<QueryObjectContainingTypeModel> ContainingTypes { get; } = containingTypes;

    public bool Equals(QueryObjectBindingModel other)
    {
        return Kind == other.Kind &&
            string.Equals(HintName, other.HintName, StringComparison.Ordinal) &&
            string.Equals(NamespaceName, other.NamespaceName, StringComparison.Ordinal) &&
            string.Equals(Modifier, other.Modifier, StringComparison.Ordinal) &&
            string.Equals(Keyword, other.Keyword, StringComparison.Ordinal) &&
            string.Equals(Name, other.Name, StringComparison.Ordinal) &&
            string.Equals(Type, other.Type, StringComparison.Ordinal) &&
            IsValueType == other.IsValueType &&
            EnableJsonQueryValues == other.EnableJsonQueryValues &&
            PolicyLocation.Equals(other.PolicyLocation) &&
            ContainingTypes.SequenceEqual(other.ContainingTypes);
    }

    public override bool Equals(object? obj)
    {
        return obj is QueryObjectBindingModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(HintName);
            hashCode = (hashCode * 397) ^ Kind.GetHashCode();
            hashCode = (hashCode * 397) ^ (NamespaceName is null ? 0 : StringComparer.Ordinal.GetHashCode(NamespaceName));
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Modifier);
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Keyword);
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Name);
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Type);
            hashCode = (hashCode * 397) ^ IsValueType.GetHashCode();
            hashCode = (hashCode * 397) ^ EnableJsonQueryValues.GetHashCode();
            hashCode = (hashCode * 397) ^ PolicyLocation.GetHashCode();
            foreach (var containingType in ContainingTypes)
            {
                hashCode = (hashCode * 397) ^ containingType.GetHashCode();
            }

            return hashCode;
        }
    }
}
