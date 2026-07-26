using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using static StaticNorth.Valiant.SourceGeneration.ValiantFingerprint;

namespace StaticNorth.Valiant.Validation.SourceGeneration;

/// <summary>
/// Generates Valiant validation implementations and dependency injection registration code.
/// </summary>
public sealed partial class ValiantGenerator
{
    private static string CreateValidatorFingerprint(
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
        ImmutableArray<DiagnosticInfo> diagnostics)
    {
        var builder = new StringBuilder();
        AppendFingerprintValue(builder, declaration.HintName);
        AppendFingerprintValue(builder, declaration.Name);
        AppendFingerprintValue(builder, declaration.Type);
        AppendFingerprintValue(builder, declaration.NamespaceName);
        AppendFingerprintValue(builder, declaration.Modifier);
        AppendFingerprintValue(builder, declaration.MetadataProviderType);
        AppendFingerprintValue(builder, declaration.Location.ToString());
        foreach (var containingType in declaration.ContainingTypes)
        {
            AppendFingerprintValue(builder, containingType.Modifier);
            AppendFingerprintValue(builder, containingType.Keyword);
            AppendFingerprintValue(builder, containingType.Name);
        }

        if (declaration.Constructor is not null)
        {
            foreach (var parameter in declaration.Constructor.Parameters)
            {
                AppendFingerprintValue(builder, parameter.Type);
                AppendFingerprintValue(builder, parameter.Name);
            }
        }

        AppendFingerprintValue(builder, modelType);
        AppendFingerprintValue(builder, metadataModelType);
        AppendFingerprintValue(builder, isModelValueType ? "value" : "reference");
        AppendFingerprintValue(builder, inheritsValidatorBase ? "base" : "no-base");
        AppendFingerprintValue(builder, implementsValidatorDefinition ? "definition" : "no-definition");
        AppendFingerprintValue(builder, hasConfigureMethod ? "configure" : "no-configure");
        AppendFingerprintValue(builder, registerAsDefault ? "default" : "non-default");
        AppendFingerprintValue(builder, configureMethodName);
        foreach (var usingDirective in usings)
        {
            AppendFingerprintValue(builder, usingDirective);
        }

        AppendValidationNodesFingerprint(builder, nodes);

        foreach (var diagnostic in diagnostics)
        {
            AppendFingerprintValue(builder, diagnostic.Descriptor.Id);
            AppendFingerprintValue(builder, diagnostic.Location.ToString());
            foreach (var messageArg in diagnostic.MessageArgs)
            {
                AppendFingerprintValue(builder, messageArg?.ToString());
            }
        }

        return builder.ToString();
    }

    private static void AppendValidationNodesFingerprint(StringBuilder builder, ImmutableArray<ValidationNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case RuleNode ruleNode:
                    AppendFingerprintValue(builder, "rule");
                    AppendRuleFingerprint(builder, ruleNode.Rule);
                    break;
                case ValidatorReuseNode reuseNode:
                    AppendFingerprintValue(builder, "reuse");
                    AppendFingerprintValue(builder, reuseNode.ValidatorExpression);
                    AppendFingerprintValue(builder, reuseNode.InstanceExpression);
                    AppendFingerprintValue(builder, reuseNode.PropertyPathPrefixExpression);
                    break;
                case IfNode ifNode:
                    AppendFingerprintValue(builder, "if");
                    AppendFingerprintValue(builder, ifNode.Condition);
                    AppendValidationNodesFingerprint(builder, ifNode.ThenNodes);
                    AppendValidationNodesFingerprint(builder, ifNode.ElseNodes);
                    break;
                case SwitchNode switchNode:
                    AppendFingerprintValue(builder, "switch");
                    AppendFingerprintValue(builder, switchNode.Expression);
                    foreach (var section in switchNode.Sections)
                    {
                        AppendFingerprintValues(builder, section.Labels);
                        AppendValidationNodesFingerprint(builder, section.Nodes);
                    }

                    break;
                case LoopNode loopNode:
                    AppendFingerprintValue(builder, "loop");
                    AppendFingerprintValue(builder, loopNode.Kind);
                    AppendFingerprintValue(builder, loopNode.Header);
                    AppendFingerprintValue(builder, loopNode.Condition);
                    AppendValidationNodesFingerprint(builder, loopNode.Nodes);
                    break;
                case FlowControlNode flowControlNode:
                    AppendFingerprintValue(builder, "flow");
                    AppendFingerprintValue(builder, flowControlNode.Kind);
                    break;
                case FailureBehaviorNode failureBehaviorNode:
                    AppendFingerprintValue(builder, "failure");
                    AppendFingerprintValue(builder, failureBehaviorNode.BehaviorExpression);
                    break;
                case RawStatementNode rawStatementNode:
                    AppendFingerprintValue(builder, "raw");
                    AppendFingerprintValue(builder, rawStatementNode.Statement);
                    break;
                case CollectionIterationNode iterationNode:
                    AppendFingerprintValue(builder, "collection");
                    AppendFingerprintValue(builder, iterationNode.CollectionExpression);
                    AppendFingerprintValue(builder, iterationNode.InstanceExpression);
                    AppendFingerprintValues(builder, iterationNode.ParentPropertyAccesses);
                    AppendFingerprintValue(builder, iterationNode.CollectionLocalName);
                    AppendFingerprintValue(builder, iterationNode.ItemLocalName);
                    AppendFingerprintValue(builder, iterationNode.IndexLocalName);
                    AppendValidationNodesFingerprint(builder, iterationNode.Nodes);
                    break;
                case ChildPropertyValidationNode childNode:
                    AppendFingerprintValue(builder, "child");
                    AppendFingerprintValue(builder, childNode.ChildExpression);
                    AppendFingerprintValue(builder, childNode.InstanceExpression);
                    AppendFingerprintValues(builder, childNode.ParentPropertyAccesses);
                    AppendFingerprintValue(builder, childNode.ChildLocalName);
                    AppendValidationNodesFingerprint(builder, childNode.Nodes);
                    break;
            }
        }
    }

    private static void AppendRuleFingerprint(StringBuilder builder, RuleModel rule)
    {
        AppendFingerprintValue(builder, rule.PropertyName);
        AppendFingerprintValue(builder, rule.PropertyNameExpression);
        AppendFingerprintValue(builder, rule.PropertyAccess);
        AppendFingerprintValues(builder, rule.ParentPropertyAccesses);
        AppendFingerprintValue(builder, rule.InstanceExpression);
        AppendFingerprintValue(builder, rule.PropertyType);
        AppendFingerprintValue(builder, rule.MetadataPropertyType);
        AppendFingerprintValue(builder, rule.IsString ? "string" : "not-string");
        AppendFingerprintValue(builder, rule.IsInteger ? "integer" : "not-integer");
        AppendFingerprintValue(builder, rule.IsNullableValueType ? "nullable-value" : "not-nullable-value");
        AppendFingerprintValue(builder, rule.FailureBehaviorExpression);
        AppendFingerprintValue(builder, rule.FailureBehaviorCallCount.ToString(CultureInfo.InvariantCulture));
        AppendFingerprintValue(builder, rule.ConditionExpression);
        AppendFingerprintValue(builder, rule.ConditionDelegateType);
        foreach (var check in rule.Checks)
        {
            AppendFingerprintValue(builder, check.Kind);
            AppendFingerprintValues(builder, check.Arguments);
            AppendFingerprintValues(builder, check.TypeArguments);
            AppendFingerprintValue(builder, check.IsAsync ? "async" : "sync");
            AppendFingerprintValue(builder, check.DelegateType);
            AppendFingerprintValue(builder, check.PredicateParameterCount.ToString(CultureInfo.InvariantCulture));
            AppendFingerprintValue(builder, check.Code);
            AppendFingerprintValue(builder, check.Message);
            AppendFingerprintValue(builder, check.Severity);
        }
    }

    private static void AnalyzeValidationSuggestions(
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        AnalyzeValidationSuggestions(nodes.ToImmutable(), diagnostics);
    }

    private static void AnalyzeValidationSuggestions(
        ImmutableArray<ValidationNode> nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case RuleNode ruleNode:
                    AnalyzeRuleSuggestions(ruleNode.Rule, diagnostics);
                    break;
                case IfNode ifNode:
                    AnalyzeValidationSuggestions(ifNode.ThenNodes, diagnostics);
                    AnalyzeValidationSuggestions(ifNode.ElseNodes, diagnostics);
                    break;
                case SwitchNode switchNode:
                    foreach (var section in switchNode.Sections)
                    {
                        AnalyzeValidationSuggestions(section.Nodes, diagnostics);
                    }

                    break;
                case LoopNode loopNode:
                    AnalyzeValidationSuggestions(loopNode.Nodes, diagnostics);
                    break;
                case CollectionIterationNode iterationNode:
                    AnalyzeValidationSuggestions(iterationNode.Nodes, diagnostics);
                    break;
                case ChildPropertyValidationNode childNode:
                    AnalyzeValidationSuggestions(childNode.Nodes, diagnostics);
                    break;
            }
        }
    }

    private static void AnalyzeRuleSuggestions(
        RuleModel rule,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        if (rule.FailureBehaviorCallCount > 1)
        {
            diagnostics.Add(new DiagnosticInfo(
                OverriddenFailureBehavior,
                rule.Location));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var check in rule.Checks)
        {
            if (!seen.Add(check.Kind) && check.Kind != "Satisfies" && check.Kind != "SatisfiesAsync")
            {
                diagnostics.Add(new DiagnosticInfo(
                    DuplicateValidationCheck,
                    check.Location,
                    check.Kind));
            }
        }

        var hasRequired = rule.Checks.Any(static check => check.Kind == "Required");
        if (!hasRequired)
        {
            return;
        }

        foreach (var check in rule.Checks)
        {
            if (check.Kind is "NotNull")
            {
                diagnostics.Add(new DiagnosticInfo(
                    RedundantValidationCheck,
                    check.Location,
                    check.Kind,
                    "Required"));
            }
        }
    }
}
