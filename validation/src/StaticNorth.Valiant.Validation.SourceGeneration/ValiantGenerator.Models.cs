global using DiagnosticInfo = StaticNorth.Valiant.SourceGeneration.ValiantDiagnosticInfo;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StaticNorth.Valiant.SourceGeneration;

namespace StaticNorth.Valiant.Validation.SourceGeneration;

/// <summary>
/// Generates Valiant validation implementations and dependency injection registration code.
/// </summary>
public sealed partial class ValiantGenerator
{
    private sealed class ValiantTypeModel(
        ValidatorModel? validator,
        DependencyInjectionPropertyValidatorModel? propertyValidator)
    {
        public ValidatorModel? Validator { get; } = validator;

        public DependencyInjectionPropertyValidatorModel? PropertyValidator { get; } = propertyValidator;
    }

    private sealed class ValidatorModel(
        ValidatorDeclarationModel declaration,
        string? modelType,
        string? metadataModelType,
        bool isModelValueType,
        bool inheritsValidatorBase,
        bool implementsValidatorDefinition,
        bool hasConfigureMethod,
        bool registerAsDefault,
        string configureMethodName,
        ImmutableArray<string> usings,
        ImmutableArray<ValidationNode> nodes,
        ImmutableArray<DiagnosticInfo> diagnostics,
        string fingerprint,
        bool generateSimpleFluent = false)
    {
        public ValidatorDeclarationModel Declaration { get; } = declaration;

        public string? ModelType { get; } = modelType;

        public string? MetadataModelType { get; } = metadataModelType;

        public bool IsModelValueType { get; } = isModelValueType;

        public bool InheritsValidatorBase { get; } = inheritsValidatorBase;

        public bool ImplementsValidatorDefinition { get; } = implementsValidatorDefinition;

        public bool HasConfigureMethod { get; } = hasConfigureMethod;

        public bool UsesRuntimeValidation => HasConfigureMethod && !generateSimpleFluent;

        public bool RegisterAsDefault { get; } = registerAsDefault;

        public string ConfigureMethodName { get; } = configureMethodName;

        public ImmutableArray<string> Usings { get; } = usings;

        public ImmutableArray<ValidationNode> Nodes { get; } = nodes;

        public ImmutableArray<DiagnosticInfo> Diagnostics { get; } = diagnostics;

        public string Fingerprint { get; } = fingerprint;
    }

    private sealed class ValidatorModelComparer : IEqualityComparer<ValidatorModel>
    {
        public static readonly ValidatorModelComparer Instance = new();

        private ValidatorModelComparer()
        {
        }

        public bool Equals(ValidatorModel? x, ValidatorModel? y)
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

        public int GetHashCode(ValidatorModel obj)
        {
            return StringComparer.Ordinal.GetHashCode(obj.Fingerprint);
        }
    }

    private sealed class ValidatorDeclarationModel(
        string hintName,
        string name,
        string type,
        string? namespaceName,
        string modifier,
        string metadataProviderType,
        ImmutableArray<ContainingTypeModel> containingTypes,
        Location? location,
        ConstructorModel? constructor)
    {
        public string HintName { get; } = hintName;

        public string Name { get; } = name;

        public string Type { get; } = type;

        public string? NamespaceName { get; } = namespaceName;

        public string Modifier { get; } = modifier;

        public string MetadataProviderType { get; } = metadataProviderType;

        public ImmutableArray<ContainingTypeModel> ContainingTypes { get; } = containingTypes;

        public ValiantDiagnosticLocation Location { get; } = ValiantDiagnosticLocation.Create(location);

        public ConstructorModel? Constructor { get; } = constructor;
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

    private sealed class ConstructorModel(ImmutableArray<ConstructorParameterModel> parameters)
    {
        public ImmutableArray<ConstructorParameterModel> Parameters { get; } = parameters;
    }

    private readonly struct ConstructorParameterModel(
        string type,
        string name) : IEquatable<ConstructorParameterModel>
    {
        public string Type { get; } = type;

        public string Name { get; } = name;

        public bool Equals(ConstructorParameterModel other)
        {
            return string.Equals(Type, other.Type, StringComparison.Ordinal) &&
                string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is ConstructorParameterModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Type) * 397) ^ StringComparer.Ordinal.GetHashCode(Name);
            }
        }
    }

    private readonly struct DependencyInjectionValidatorModel(
        string validatorType,
        string modelType,
        string metadataProviderType,
        bool registerAsDefault) : IEquatable<DependencyInjectionValidatorModel>
    {
        public string ValidatorType { get; } = validatorType;

        public string ModelType { get; } = modelType;

        public string MetadataProviderType { get; } = metadataProviderType;

        public bool RegisterAsDefault { get; } = registerAsDefault;

        public bool Equals(DependencyInjectionValidatorModel other)
        {
            return string.Equals(ValidatorType, other.ValidatorType, StringComparison.Ordinal) &&
                string.Equals(ModelType, other.ModelType, StringComparison.Ordinal) &&
                string.Equals(MetadataProviderType, other.MetadataProviderType, StringComparison.Ordinal) &&
                RegisterAsDefault == other.RegisterAsDefault;
        }

        public override bool Equals(object? obj)
        {
            return obj is DependencyInjectionValidatorModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StringComparer.Ordinal.GetHashCode(ValidatorType);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(ModelType);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(MetadataProviderType);
                hashCode = (hashCode * 397) ^ RegisterAsDefault.GetHashCode();
                return hashCode;
            }
        }
    }

    private readonly struct DependencyInjectionPropertyValidatorModel(
        string validatorType,
        ImmutableArray<string> propertyTypes) : IEquatable<DependencyInjectionPropertyValidatorModel>
    {
        public string ValidatorType { get; } = validatorType;

        public ImmutableArray<string> PropertyTypes { get; } = propertyTypes;

        public bool Equals(DependencyInjectionPropertyValidatorModel other)
        {
            return string.Equals(ValidatorType, other.ValidatorType, StringComparison.Ordinal) &&
                PropertyTypes.SequenceEqual(other.PropertyTypes, StringComparer.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is DependencyInjectionPropertyValidatorModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StringComparer.Ordinal.GetHashCode(ValidatorType);
                foreach (var propertyType in PropertyTypes)
                {
                    hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(propertyType);
                }

                return hashCode;
            }
        }
    }

    private abstract class ValidationNode;

    private sealed class RuleNode(RuleModel rule) : ValidationNode
    {
        public RuleModel Rule { get; } = rule;
    }

    private sealed class ValidatorReuseNode(
        string validatorExpression,
        string instanceExpression,
        string? propertyPathPrefixExpression) : ValidationNode
    {
        public string ValidatorExpression { get; } = validatorExpression;

        public string InstanceExpression { get; } = instanceExpression;

        public string? PropertyPathPrefixExpression { get; } = propertyPathPrefixExpression;
    }

    private sealed class IfNode(
        string condition,
        ImmutableArray<ValidationNode> thenNodes,
        ImmutableArray<ValidationNode> elseNodes) : ValidationNode
    {
        public string Condition { get; } = condition;

        public ImmutableArray<ValidationNode> ThenNodes { get; } = thenNodes;

        public ImmutableArray<ValidationNode> ElseNodes { get; } = elseNodes;
    }

    private sealed class SwitchNode(
        string expression,
        ImmutableArray<SwitchSectionModel> sections,
        string? label) : ValidationNode
    {
        public string Expression { get; } = expression;

        public ImmutableArray<SwitchSectionModel> Sections { get; } = sections;

        public string? Label { get; } = label;
    }

    private sealed class SwitchSectionModel(
        ImmutableArray<string> labels,
        ImmutableArray<ValidationNode> nodes)
    {
        public ImmutableArray<string> Labels { get; } = labels;

        public ImmutableArray<ValidationNode> Nodes { get; } = nodes;
    }

    private sealed class LoopNode(
        string kind,
        string? header,
        string? condition,
        ImmutableArray<ValidationNode> nodes,
        string? label) : ValidationNode
    {
        public string Kind { get; } = kind;

        public string? Header { get; } = header;

        public string? Condition { get; } = condition;

        public ImmutableArray<ValidationNode> Nodes { get; } = nodes;

        public string? Label { get; } = label;
    }

    private sealed class FlowControlNode(string kind, string? label) : ValidationNode
    {
        public string Kind { get; } = kind;

        public string? Label { get; } = label;
    }

    private sealed class FailureBehaviorNode(string behaviorExpression) : ValidationNode
    {
        public string BehaviorExpression { get; } = behaviorExpression;
    }

    private sealed class RawStatementNode(string statement) : ValidationNode
    {
        public string Statement { get; } = statement;
    }

    private sealed class CollectionIterationNode(
        string collectionExpression,
        string instanceExpression,
        ImmutableArray<string> parentPropertyAccesses,
        string collectionLocalName,
        string itemLocalName,
        string indexLocalName,
        ImmutableArray<ValidationNode> nodes) : ValidationNode
    {
        public string CollectionExpression { get; } = collectionExpression;

        public string InstanceExpression { get; } = instanceExpression;

        public ImmutableArray<string> ParentPropertyAccesses { get; } = parentPropertyAccesses;

        public string CollectionLocalName { get; } = collectionLocalName;

        public string ItemLocalName { get; } = itemLocalName;

        public string IndexLocalName { get; } = indexLocalName;

        public ImmutableArray<ValidationNode> Nodes { get; } = nodes;
    }

    private sealed class ChildPropertyValidationNode(
        string childExpression,
        string instanceExpression,
        ImmutableArray<string> parentPropertyAccesses,
        string childLocalName,
        ImmutableArray<ValidationNode> nodes) : ValidationNode
    {
        public string ChildExpression { get; } = childExpression;

        public string InstanceExpression { get; } = instanceExpression;

        public ImmutableArray<string> ParentPropertyAccesses { get; } = parentPropertyAccesses;

        public string ChildLocalName { get; } = childLocalName;

        public ImmutableArray<ValidationNode> Nodes { get; } = nodes;
    }

    private sealed class ParseScope(
        string currentContextParameterName,
        string currentInstanceExpression,
        string? propertyPathPrefixExpression,
        ImmutableDictionary<string, string> contextInstances,
        ImmutableDictionary<string, string> identifiers)
    {
        public string CurrentContextParameterName { get; } = currentContextParameterName;

        public string CurrentInstanceExpression { get; } = currentInstanceExpression;

        public string? PropertyPathPrefixExpression { get; } = propertyPathPrefixExpression;

        public ImmutableDictionary<string, string> ContextInstances { get; } = contextInstances;

        public ImmutableDictionary<string, string> Identifiers { get; } = identifiers;

        public static ParseScope Root(string contextParameterName)
        {
            return new ParseScope(
                contextParameterName,
                "instance",
                null,
                ImmutableDictionary.Create<string, string>().Add(contextParameterName, "instance"),
                ImmutableDictionary<string, string>.Empty);
        }

        public ParseScope WithItemContext(
            string itemContextParameterName,
            string itemInstanceExpression,
            string? collectionParameterName,
            string collectionExpression,
            string propertyPathPrefixExpression)
        {
            var contextInstances = ContextInstances.SetItem(itemContextParameterName, itemInstanceExpression);
            var identifiers = Identifiers;
            if (!string.IsNullOrEmpty(collectionParameterName))
            {
                identifiers = identifiers.SetItem(collectionParameterName!, collectionExpression);
            }

            return new ParseScope(
                itemContextParameterName,
                itemInstanceExpression,
                propertyPathPrefixExpression,
                contextInstances,
                identifiers);
        }
    }

    private sealed class RuleModel(
        string propertyName,
        string propertyNameExpression,
        string propertyAccess,
        ImmutableArray<string> parentPropertyAccesses,
        string instanceExpression,
        string propertyType,
        string metadataPropertyType,
        bool isString,
        bool isInteger,
        bool isNullableValueType,
        string? failureBehaviorExpression,
        int failureBehaviorCallCount,
        string? conditionExpression,
        string? conditionDelegateType,
        Location location,
        ImmutableArray<RuleCheck> checks)
    {
        public string PropertyName { get; } = propertyName;

        public string PropertyNameExpression { get; } = propertyNameExpression;

        public string PropertyAccess { get; } = propertyAccess;

        public ImmutableArray<string> ParentPropertyAccesses { get; } = parentPropertyAccesses;

        public string InstanceExpression { get; } = instanceExpression;

        public string PropertyType { get; } = propertyType;

        public string MetadataPropertyType { get; } = metadataPropertyType;

        public bool IsString { get; } = isString;

        public bool IsInteger { get; } = isInteger;

        public bool IsNullableValueType { get; } = isNullableValueType;

        public string? FailureBehaviorExpression { get; } = failureBehaviorExpression;

        public int FailureBehaviorCallCount { get; } = failureBehaviorCallCount;

        public string? ConditionExpression { get; } = conditionExpression;

        public string? ConditionDelegateType { get; } = conditionDelegateType;

        public ValiantDiagnosticLocation Location { get; } = ValiantDiagnosticLocation.Create(location);

        public ImmutableArray<RuleCheck> Checks { get; } = checks;
    }

    private sealed class RuleCheck(
        string kind,
        ImmutableArray<string> arguments,
        ImmutableArray<string> typeArguments,
        bool isAsync,
        string? delegateType,
        int predicateParameterCount,
        string? code,
        string? message,
        string? severity,
        Location location)
    {
        public string Kind { get; } = kind;

        public ImmutableArray<string> Arguments { get; } = arguments;

        public ImmutableArray<string> TypeArguments { get; } = typeArguments;

        public bool IsAsync { get; } = isAsync;

        public string? DelegateType { get; } = delegateType;

        public int PredicateParameterCount { get; } = predicateParameterCount;

        public string? Code { get; } = code;

        public string? Message { get; } = message;

        public string? Severity { get; } = severity;

        public ValiantDiagnosticLocation Location { get; } = ValiantDiagnosticLocation.Create(location);
    }

    private sealed class PropertySelector(
        string propertyName,
        string propertyAccess,
        ImmutableArray<string> parentPropertyAccesses,
        string propertyType,
        string metadataPropertyType,
        bool isString,
        bool isInteger,
        bool isNullableValueType)
    {
        public string PropertyName { get; } = propertyName;

        public string PropertyAccess { get; } = propertyAccess;

        public ImmutableArray<string> ParentPropertyAccesses { get; } = parentPropertyAccesses;

        public string PropertyType { get; } = propertyType;

        public string MetadataPropertyType { get; } = metadataPropertyType;

        public bool IsString { get; } = isString;

        public bool IsInteger { get; } = isInteger;

        public bool IsNullableValueType { get; } = isNullableValueType;
    }

    private sealed class CallInfo(
        string name,
        ExpressionSyntax receiver,
        ImmutableArray<TypeSyntax> typeArguments,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        InvocationExpressionSyntax invocation)
    {
        public string Name { get; } = name;

        public ExpressionSyntax Receiver { get; } = receiver;

        public ImmutableArray<TypeSyntax> TypeArguments { get; } = typeArguments;

        public SeparatedSyntaxList<ArgumentSyntax> Arguments { get; } = arguments;

        public InvocationExpressionSyntax Invocation { get; } = invocation;
    }

    private sealed class ValidationContextRewriter(ParseScope scope) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.Name.Identifier.ValueText == "Instance" &&
                node.Expression is IdentifierNameSyntax identifier &&
                scope.ContextInstances.TryGetValue(identifier.Identifier.ValueText, out var instanceExpression))
            {
                return SyntaxFactory.ParseExpression(instanceExpression).WithTriviaFrom(node);
            }

            return base.VisitMemberAccessExpression(node);
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (scope.Identifiers.TryGetValue(node.Identifier.ValueText, out var replacement))
            {
                return SyntaxFactory.ParseExpression(replacement).WithTriviaFrom(node);
            }

            return base.VisitIdentifierName(node);
        }
    }

}
