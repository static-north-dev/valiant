using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Valiant.SourceGeneration.ValiantNativeControlFlow;

namespace Valiant.Validation.SourceGeneration;

/// <summary>
/// Generates Valiant validation implementations and dependency injection registration code.
/// </summary>
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
                else if (TryParseChildPropertyValidation(invocation, scope, semanticModel, cancellationToken, diagnostics, out var childNode, out var childDiagnostic))
                {
                    nodes.Add(childNode);
                }
                else if (TryParseItemRule(invocation, scope, semanticModel, cancellationToken, out var itemRule, out var itemDiagnostic))
                {
                    nodes.Add(new RuleNode(itemRule));
                }
                else if (TryParseBarePropertyDeclaration(invocation, scope))
                {
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
                else if (childDiagnostic is not null)
                {
                    diagnostics.Add(childDiagnostic);
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

            case ReturnStatementSyntax returnStatement when IsContextReturn(returnStatement, scope.CurrentContextParameterName):
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

            var sectionNodes = ImmutableArray.CreateBuilder<ValidationNode>();
            ParseStatements(section.Statements, scope, semanticModel, cancellationToken, sectionNodes, diagnostics);
            sections.Add(new SwitchSectionModel(
                CreateSwitchLabels(section, node => RewriteForValidation(node, scope)),
                sectionNodes.ToImmutable()));
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
            CreateForEachHeader(forEachStatement, node => RewriteForValidation(node, scope)),
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
        nodes.Add(new LoopNode(
            "for",
            CreateForHeader(forStatement, node => RewriteForValidation(node, scope)),
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
            CreateWhileCondition(whileStatement, node => RewriteForValidation(node, scope)),
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
            CreateDoCondition(doStatement, node => RewriteForValidation(node, scope)),
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
