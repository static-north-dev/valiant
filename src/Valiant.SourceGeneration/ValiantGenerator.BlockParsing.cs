using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Valiant.SourceGeneration;

public sealed partial class ValiantGenerator
{
    private static void ParseStatements(
        SyntaxList<StatementSyntax> statements,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        foreach (var statement in statements)
        {
            ParseStatement(statement, scope, semanticModel, cancellationToken, nodes, diagnostics);
        }
    }

    private static void ParseStatement(
        StatementSyntax statement,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (statement)
        {
            case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }:
                if (TryParseFailureBehavior(invocation, scope, out var failureBehaviorNode))
                {
                    nodes.Add(failureBehaviorNode);
                }
                else if (TryParseCollectionIteration(invocation, scope, semanticModel, cancellationToken, diagnostics, out var iterationNode, out var iterationDiagnostic))
                {
                    nodes.Add(iterationNode);
                }
                else if (TryParseItemRule(invocation, scope, semanticModel, cancellationToken, out var itemRule, out var itemDiagnostic))
                {
                    nodes.Add(new RuleNode(itemRule));
                }
                else if (TryParseRule(invocation, scope, semanticModel, cancellationToken, out var rule, out var diagnostic))
                {
                    nodes.Add(new RuleNode(rule));
                }
                else if (TryParseValidatorReuse(invocation, scope, semanticModel, cancellationToken, out var reuseNode, out var reuseDiagnostic))
                {
                    nodes.Add(reuseNode);
                }
                else if (iterationDiagnostic is not null)
                {
                    diagnostics.Add(iterationDiagnostic);
                }
                else if (itemDiagnostic is not null)
                {
                    diagnostics.Add(itemDiagnostic);
                }
                else if (reuseDiagnostic is not null)
                {
                    diagnostics.Add(reuseDiagnostic);
                }
                else if (diagnostic is not null)
                {
                    diagnostics.Add(diagnostic);
                }

                return;

            case IfStatementSyntax ifStatement:
                ParseIfStatement(ifStatement, scope, semanticModel, cancellationToken, nodes, diagnostics);
                return;

            case SwitchStatementSyntax switchStatement:
                ParseSwitchStatement(switchStatement, scope, semanticModel, cancellationToken, nodes, diagnostics);
                return;

            case ForEachStatementSyntax forEachStatement:
                ParseForEachStatement(forEachStatement, scope, semanticModel, cancellationToken, nodes, diagnostics);
                return;

            case ForStatementSyntax forStatement:
                ParseForStatement(forStatement, scope, semanticModel, cancellationToken, nodes, diagnostics);
                return;

            case WhileStatementSyntax whileStatement:
                ParseWhileStatement(whileStatement, scope, semanticModel, cancellationToken, nodes, diagnostics);
                return;

            case DoStatementSyntax doStatement:
                ParseDoStatement(doStatement, scope, semanticModel, cancellationToken, nodes, diagnostics);
                return;

            case BlockSyntax block:
                ParseStatements(block.Statements, scope, semanticModel, cancellationToken, nodes, diagnostics);
                return;

            case ReturnStatementSyntax { Expression: not null } returnStatement when IsIdentifier(returnStatement.Expression, scope.CurrentContextParameterName):
            case ReturnStatementSyntax { Expression: null }:
                return;

            case BreakStatementSyntax:
                nodes.Add(new FlowControlNode("break"));
                return;

            case ContinueStatementSyntax:
                nodes.Add(new FlowControlNode("continue"));
                return;

            case LocalDeclarationStatementSyntax localDeclaration:
                nodes.Add(new RawStatementNode(RewriteForValidation(localDeclaration, scope)));
                return;

            case ExpressionStatementSyntax expressionStatement when IsSupportedRawExpression(expressionStatement.Expression):
                nodes.Add(new RawStatementNode(RewriteForValidation(expressionStatement, scope)));
                return;

            default:
                diagnostics.Add(new DiagnosticInfo(
                    UnsupportedValidationDeclaration,
                    statement.GetLocation(),
                    "Only Property declarations, UseValidator declarations, if/else statements, switch statements, loops, blocks, break/continue statements, and return statements are supported in validator declarations."));
                return;
        }
    }

    private static void ParseIfStatement(
        IfStatementSyntax ifStatement,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var thenNodes = ParseNestedStatement(ifStatement.Statement, scope, semanticModel, cancellationToken, diagnostics);
        var elseNodes = ifStatement.Else is null
            ? ImmutableArray<ValidationNode>.Empty
            : ParseNestedStatement(ifStatement.Else.Statement, scope, semanticModel, cancellationToken, diagnostics);

        nodes.Add(new IfNode(
            RewriteForValidation(ifStatement.Condition, scope),
            thenNodes,
            elseNodes));
    }

    private static void ParseSwitchStatement(
        SwitchStatementSyntax switchStatement,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var sections = ImmutableArray.CreateBuilder<SwitchSectionModel>();
        foreach (var section in switchStatement.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var labels = ImmutableArray.CreateBuilder<string>();
            foreach (var label in section.Labels)
            {
                labels.Add(RewriteForValidation(label, scope));
            }

            var sectionNodes = ImmutableArray.CreateBuilder<ValidationNode>();
            ParseStatements(section.Statements, scope, semanticModel, cancellationToken, sectionNodes, diagnostics);
            sections.Add(new SwitchSectionModel(labels.ToImmutable(), sectionNodes.ToImmutable()));
        }

        nodes.Add(new SwitchNode(
            RewriteForValidation(switchStatement.Expression, scope),
            sections.ToImmutable()));
    }

    private static void ParseForEachStatement(
        ForEachStatementSyntax forEachStatement,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        nodes.Add(new LoopNode(
            "foreach",
            forEachStatement.Type + " " + forEachStatement.Identifier.ValueText + " in " + RewriteForValidation(forEachStatement.Expression, scope),
            null,
            ParseNestedStatement(forEachStatement.Statement, scope, semanticModel, cancellationToken, diagnostics)));
    }

    private static void ParseForStatement(
        ForStatementSyntax forStatement,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var initializer = forStatement.Declaration is not null
            ? RewriteForValidation(forStatement.Declaration, scope)
            : string.Join(", ", forStatement.Initializers.Select(initializer => RewriteForValidation(initializer, scope)));
        var condition = forStatement.Condition is null
            ? string.Empty
            : RewriteForValidation(forStatement.Condition, scope);
        var incrementors = string.Join(", ", forStatement.Incrementors.Select(incrementor => RewriteForValidation(incrementor, scope)));

        nodes.Add(new LoopNode(
            "for",
            initializer + "; " + condition + "; " + incrementors,
            null,
            ParseNestedStatement(forStatement.Statement, scope, semanticModel, cancellationToken, diagnostics)));
    }

    private static void ParseWhileStatement(
        WhileStatementSyntax whileStatement,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        nodes.Add(new LoopNode(
            "while",
            RewriteForValidation(whileStatement.Condition, scope),
            null,
            ParseNestedStatement(whileStatement.Statement, scope, semanticModel, cancellationToken, diagnostics)));
    }

    private static void ParseDoStatement(
        DoStatementSyntax doStatement,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        nodes.Add(new LoopNode(
            "do",
            null,
            RewriteForValidation(doStatement.Condition, scope),
            ParseNestedStatement(doStatement.Statement, scope, semanticModel, cancellationToken, diagnostics)));
    }

    private static ImmutableArray<ValidationNode> ParseNestedStatement(
        StatementSyntax statement,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var nodes = ImmutableArray.CreateBuilder<ValidationNode>();
        ParseStatement(statement, scope, semanticModel, cancellationToken, nodes, diagnostics);
        return nodes.ToImmutable();
    }
}
