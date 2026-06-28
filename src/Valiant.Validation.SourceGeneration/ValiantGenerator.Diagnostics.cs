using System.Collections.Immutable;

namespace Valiant.Validation.SourceGeneration;

public sealed partial class ValiantGenerator
{
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
