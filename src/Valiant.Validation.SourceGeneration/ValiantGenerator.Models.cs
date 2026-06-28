using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Valiant.Validation.SourceGeneration;

public sealed partial class ValiantGenerator
{
    private sealed class ValidatorModel(
        INamedTypeSymbol validatorSymbol,
        ITypeSymbol? modelType,
        string configureMethodName,
        ImmutableArray<string> usings,
        ImmutableArray<ValidationNode> nodes,
        ImmutableArray<DiagnosticInfo> diagnostics)
    {
        public INamedTypeSymbol ValidatorSymbol { get; } = validatorSymbol;

        public ITypeSymbol? ModelType { get; } = modelType;

        public string ConfigureMethodName { get; } = configureMethodName;

        public ImmutableArray<string> Usings { get; } = usings;

        public ImmutableArray<ValidationNode> Nodes { get; } = nodes;

        public ImmutableArray<DiagnosticInfo> Diagnostics { get; } = diagnostics;
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
        ImmutableArray<SwitchSectionModel> sections) : ValidationNode
    {
        public string Expression { get; } = expression;

        public ImmutableArray<SwitchSectionModel> Sections { get; } = sections;
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
        ImmutableArray<ValidationNode> nodes) : ValidationNode
    {
        public string Kind { get; } = kind;

        public string? Header { get; } = header;

        public string? Condition { get; } = condition;

        public ImmutableArray<ValidationNode> Nodes { get; } = nodes;
    }

    private sealed class FlowControlNode(string kind) : ValidationNode
    {
        public string Kind { get; } = kind;
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
        string collectionLocalName,
        string itemLocalName,
        string indexLocalName,
        ImmutableArray<ValidationNode> nodes) : ValidationNode
    {
        public string CollectionExpression { get; } = collectionExpression;

        public string CollectionLocalName { get; } = collectionLocalName;

        public string ItemLocalName { get; } = itemLocalName;

        public string IndexLocalName { get; } = indexLocalName;

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
        string instanceExpression,
        string propertyType,
        string metadataPropertyType,
        bool isString,
        bool isInteger,
        string? failureBehaviorExpression,
        int failureBehaviorCallCount,
        Location location,
        ImmutableArray<RuleCheck> checks)
    {
        public string PropertyName { get; } = propertyName;

        public string PropertyNameExpression { get; } = propertyNameExpression;

        public string PropertyAccess { get; } = propertyAccess;

        public string InstanceExpression { get; } = instanceExpression;

        public string PropertyType { get; } = propertyType;

        public string MetadataPropertyType { get; } = metadataPropertyType;

        public bool IsString { get; } = isString;

        public bool IsInteger { get; } = isInteger;

        public string? FailureBehaviorExpression { get; } = failureBehaviorExpression;

        public int FailureBehaviorCallCount { get; } = failureBehaviorCallCount;

        public Location Location { get; } = location;

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

        public Location Location { get; } = location;
    }

    private sealed class PropertySelector(
        string propertyName,
        string propertyAccess,
        string propertyType,
        string metadataPropertyType,
        bool isString,
        bool isInteger)
    {
        public string PropertyName { get; } = propertyName;

        public string PropertyAccess { get; } = propertyAccess;

        public string PropertyType { get; } = propertyType;

        public string MetadataPropertyType { get; } = metadataPropertyType;

        public bool IsString { get; } = isString;

        public bool IsInteger { get; } = isInteger;
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

    private sealed class DiagnosticInfo(
        DiagnosticDescriptor descriptor,
        Location? location,
        params object[] messageArgs)
    {
        public DiagnosticDescriptor Descriptor { get; } = descriptor;

        public Location? Location { get; } = location;

        public object[] MessageArgs { get; } = messageArgs;

        public Diagnostic Create()
        {
            return Diagnostic.Create(Descriptor, Location, MessageArgs);
        }
    }
}
