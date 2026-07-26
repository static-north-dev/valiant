using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StaticNorth.Valiant.SourceGeneration;

namespace StaticNorth.Valiant.Mapping.SourceGeneration;

/// <summary>
/// Generates Valiant mapper implementations and dependency injection registration code.
/// </summary>
public sealed partial class ValiantMappingGenerator
{
    private sealed class MapperModel(
        MapperDeclarationModel declaration,
        MappingModel? mapping,
        DependencyInjectionMapperModel? dependencyInjection,
        ImmutableArray<DiagnosticInfo> diagnostics,
        string fingerprint)
    {
        public MapperDeclarationModel Declaration { get; } = declaration;

        public MappingModel? Mapping { get; } = mapping;

        public DependencyInjectionMapperModel? DependencyInjection { get; } = dependencyInjection;

        public ImmutableArray<DiagnosticInfo> Diagnostics { get; } = diagnostics;

        public string Fingerprint { get; } = fingerprint;
    }

    private sealed class MapperModelComparer : IEqualityComparer<MapperModel>
    {
        public static readonly MapperModelComparer Instance = new();

        private MapperModelComparer()
        {
        }

        public bool Equals(MapperModel? x, MapperModel? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.Fingerprint, y.Fingerprint, StringComparison.Ordinal);
        }

        public int GetHashCode(MapperModel obj)
        {
            return StringComparer.Ordinal.GetHashCode(obj.Fingerprint);
        }
    }

    private sealed class MapperDeclarationModel(
        string hintName,
        string name,
        string? namespaceName,
        string modifier,
        string keyword,
        ImmutableArray<ContainingTypeModel> containingTypes,
        Location? location,
        bool hasInstanceConstructor,
        ImmutableArray<MapperConstructorModel> userConstructors,
        ImmutableArray<string> constructorSignatures)
    {
        public string HintName { get; } = hintName;

        public string Name { get; } = name;

        public string? NamespaceName { get; } = namespaceName;

        public string Modifier { get; } = modifier;

        public string Keyword { get; } = keyword;

        public ImmutableArray<ContainingTypeModel> ContainingTypes { get; } = containingTypes;

        public ValiantDiagnosticLocation Location { get; } = ValiantDiagnosticLocation.Create(location);

        public bool HasInstanceConstructor { get; } = hasInstanceConstructor;

        public ImmutableArray<MapperConstructorModel> UserConstructors { get; } = userConstructors;

        public ImmutableArray<string> ConstructorSignatures { get; } = constructorSignatures;
    }

    private sealed class ContainingTypeModel(
        string modifier,
        string keyword,
        string name)
    {
        public string Modifier { get; } = modifier;

        public string Keyword { get; } = keyword;

        public string Name { get; } = name;
    }

    private sealed class MapperConstructorModel(ImmutableArray<MapperConstructorParameterModel> parameters)
    {
        public ImmutableArray<MapperConstructorParameterModel> Parameters { get; } = parameters;
    }

    private sealed class MapperConstructorParameterModel(string typeName, string name)
    {
        public string TypeName { get; } = typeName;

        public string Name { get; } = name;
    }

    private sealed class MappingPair(ITypeSymbol sourceType, ITypeSymbol targetType)
    {
        public ITypeSymbol SourceType { get; } = sourceType;

        public ITypeSymbol TargetType { get; } = targetType;
    }

    private sealed class MappingModel(
        string sourceType,
        string targetType,
        bool isSourceValueType,
        bool inheritsMapperBase,
        bool hasConfigureMethod,
        bool usesServiceProviderMapper,
        ConstructorMappingModel constructor,
        ImmutableArray<MemberAssignmentModel> assignments)
    {
        public string SourceType { get; } = sourceType;

        public string TargetType { get; } = targetType;

        public bool IsSourceValueType { get; } = isSourceValueType;

        public bool InheritsMapperBase { get; } = inheritsMapperBase;

        public bool HasConfigureMethod { get; } = hasConfigureMethod;

        public bool UsesServiceProviderMapper { get; } = usesServiceProviderMapper;

        public ConstructorMappingModel Constructor { get; } = constructor;

        public ImmutableArray<MemberAssignmentModel> Assignments { get; } = assignments;
    }

    private sealed class ConstructorMappingModel(ImmutableArray<ConstructorArgumentModel> arguments)
    {
        public ImmutableArray<ConstructorArgumentModel> Arguments { get; } = arguments;
    }

    private enum NullHandlingPolicy
    {
        Default,
        MapNulls,
        IgnoreSourceNulls
    }

    private sealed class ConstructorArgumentModel(string parameterName, string sourceExpression)
    {
        public string ParameterName { get; } = parameterName;

        public string SourceExpression { get; } = sourceExpression;
    }

    private sealed class MemberAssignmentModel(
        string targetMemberName,
        string sourceExpression,
        string? conditionExpression)
    {
        public string TargetMemberName { get; } = targetMemberName;

        public string SourceExpression { get; } = sourceExpression;

        public string? ConditionExpression { get; } = conditionExpression;
    }

    private sealed class SourceMemberModel(string name, ITypeSymbol type, string expression)
    {
        public string Name { get; } = name;

        public ITypeSymbol Type { get; } = type;

        public string Expression { get; } = expression;
    }

    private sealed class SourceMemberIndex
    {
        private readonly Dictionary<string, SourceMemberModel> _exactMembers;
        private readonly Dictionary<string, SourceMemberModel> _caseInsensitiveMembers;

        public SourceMemberIndex(ImmutableArray<SourceMemberModel> members)
        {
            Members = members;
            _exactMembers = new Dictionary<string, SourceMemberModel>(members.Length, StringComparer.Ordinal);
            _caseInsensitiveMembers = new Dictionary<string, SourceMemberModel>(members.Length, StringComparer.OrdinalIgnoreCase);
            foreach (var member in members)
            {
                if (!_exactMembers.ContainsKey(member.Name))
                {
                    _exactMembers.Add(member.Name, member);
                }

                if (!_caseInsensitiveMembers.ContainsKey(member.Name))
                {
                    _caseInsensitiveMembers.Add(member.Name, member);
                }
            }
        }

        public ImmutableArray<SourceMemberModel> Members { get; }

        public bool TryGet(string name, out SourceMemberModel sourceMember)
        {
            return _exactMembers.TryGetValue(name, out sourceMember!) ||
                _caseInsensitiveMembers.TryGetValue(name, out sourceMember!);
        }
    }

    private sealed class TargetMemberModel(string name, ITypeSymbol type, bool isRequired, bool canAssignAfterConstruction, Location? location)
    {
        public string Name { get; } = name;

        public ITypeSymbol Type { get; } = type;

        public bool IsRequired { get; } = isRequired;

        public bool CanAssignAfterConstruction { get; } = canAssignAfterConstruction;

        public Location? Location { get; } = location;
    }

    private sealed class ConfigureModel(
        bool disablesConventionMapping,
        ImmutableArray<ExplicitMemberMappingModel> explicitMappings,
        ImmutableArray<IgnoredMemberMappingModel> ignoredMappings,
        ImmutableArray<MapperReuseModel> mapperReuses)
    {
        public static ConfigureModel Empty { get; } = new(
            false,
            ImmutableArray<ExplicitMemberMappingModel>.Empty,
            ImmutableArray<IgnoredMemberMappingModel>.Empty,
            ImmutableArray<MapperReuseModel>.Empty);

        public static ConfigureModel ConventionDisabled { get; } = new(
            true,
            ImmutableArray<ExplicitMemberMappingModel>.Empty,
            ImmutableArray<IgnoredMemberMappingModel>.Empty,
            ImmutableArray<MapperReuseModel>.Empty);

        public bool DisablesConventionMapping { get; } = disablesConventionMapping;

        public ImmutableArray<ExplicitMemberMappingModel> ExplicitMappings { get; } = explicitMappings;

        public ImmutableArray<IgnoredMemberMappingModel> IgnoredMappings { get; } = ignoredMappings;

        public ImmutableArray<MapperReuseModel> MapperReuses { get; } = mapperReuses;
    }

    private sealed class PendingExplicitMappingModel(
        string targetMemberName,
        string sourceExpression,
        ITypeSymbol sourceType,
        Location location)
    {
        public string TargetMemberName { get; } = targetMemberName;

        public string SourceExpression { get; } = sourceExpression;

        public ITypeSymbol SourceType { get; } = sourceType;

        public Location Location { get; } = location;

        public string? FallbackExpression { get; set; }

        public NullHandlingPolicy NullHandling { get; set; } = NullHandlingPolicy.Default;
    }

    private sealed class ExplicitMemberMappingModel(
        string targetMemberName,
        string sourceExpression,
        ITypeSymbol sourceType,
        string? fallbackExpression,
        NullHandlingPolicy nullHandling,
        string? conditionExpression,
        Location? location)
    {
        public string TargetMemberName { get; } = targetMemberName;

        public string SourceExpression { get; } = sourceExpression;

        public ITypeSymbol SourceType { get; } = sourceType;

        public string? FallbackExpression { get; } = fallbackExpression;

        public NullHandlingPolicy NullHandling { get; } = nullHandling;

        public string? ConditionExpression { get; } = conditionExpression;

        public Location? Location { get; } = location;
    }

    private sealed class IgnoredMemberMappingModel(
        string targetMemberName,
        string? conditionExpression,
        Location? location)
    {
        public string TargetMemberName { get; } = targetMemberName;

        public string? ConditionExpression { get; } = conditionExpression;

        public Location? Location { get; } = location;
    }

    private sealed class MapperReuseModel(
        string mapperExpression,
        ITypeSymbol sourceType,
        ITypeSymbol targetType)
    {
        public string MapperExpression { get; } = mapperExpression;

        public ITypeSymbol SourceType { get; } = sourceType;

        public ITypeSymbol TargetType { get; } = targetType;
    }

    private readonly struct DependencyInjectionMapperModel(
        string mapperType,
        string sourceType,
        string targetType) : IEquatable<DependencyInjectionMapperModel>
    {
        public string MapperType { get; } = mapperType;

        public string SourceType { get; } = sourceType;

        public string TargetType { get; } = targetType;

        public bool Equals(DependencyInjectionMapperModel other)
        {
            return string.Equals(MapperType, other.MapperType, StringComparison.Ordinal) &&
                string.Equals(SourceType, other.SourceType, StringComparison.Ordinal) &&
                string.Equals(TargetType, other.TargetType, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is DependencyInjectionMapperModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StringComparer.Ordinal.GetHashCode(MapperType);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(SourceType);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(TargetType);
                return hashCode;
            }
        }
    }

    private sealed class LambdaParameterRewriter(string parameterName, string replacementName) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            return string.Equals(node.Identifier.ValueText, parameterName, StringComparison.Ordinal)
                ? SyntaxFactory.IdentifierName(replacementName)
                : base.VisitIdentifierName(node);
        }
    }

    private sealed class MappingContextSourceRewriter(string contextParameterName) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.Expression is IdentifierNameSyntax identifier &&
                string.Equals(identifier.Identifier.ValueText, contextParameterName, StringComparison.Ordinal) &&
                string.Equals(node.Name.Identifier.ValueText, "Source", StringComparison.Ordinal))
            {
                return SyntaxFactory.IdentifierName("source");
            }

            return base.VisitMemberAccessExpression(node);
        }
    }
}
