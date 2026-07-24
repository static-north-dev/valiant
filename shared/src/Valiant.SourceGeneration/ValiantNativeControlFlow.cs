using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Valiant.SourceGeneration;

internal static class ValiantNativeControlFlow
{
    public static string? CombineConditions(string? left, string? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return "(" + left + ") && (" + right + ")";
    }

    public static string NegateCondition(string condition)
    {
        return "!(" + condition + ")";
    }

    public static bool IsContextReturn(ReturnStatementSyntax returnStatement, string contextParameterName)
    {
        return returnStatement.Expression is null ||
            returnStatement.Expression is IdentifierNameSyntax identifier &&
            string.Equals(identifier.Identifier.ValueText, contextParameterName, StringComparison.Ordinal);
    }

    public static ImmutableArray<string> CreateSwitchLabels(
        SwitchSectionSyntax section,
        Func<SyntaxNode, string> rewrite)
    {
        var labels = ImmutableArray.CreateBuilder<string>();
        foreach (var label in section.Labels)
        {
            labels.Add(rewrite(label));
        }

        return labels.ToImmutable();
    }

    public static string CreateForEachHeader(
        ForEachStatementSyntax forEachStatement,
        Func<SyntaxNode, string> rewrite)
    {
        return forEachStatement.Type + " " + forEachStatement.Identifier.ValueText + " in " + rewrite(forEachStatement.Expression);
    }

    public static string CreateForHeader(
        ForStatementSyntax forStatement,
        Func<SyntaxNode, string> rewrite)
    {
        var initializer = forStatement.Declaration is not null
            ? rewrite(forStatement.Declaration)
            : string.Join(", ", forStatement.Initializers.Select(rewrite));
        var condition = forStatement.Condition is null
            ? string.Empty
            : rewrite(forStatement.Condition);
        var incrementors = string.Join(", ", forStatement.Incrementors.Select(rewrite));

        return initializer + "; " + condition + "; " + incrementors;
    }

    public static string CreateWhileCondition(
        WhileStatementSyntax whileStatement,
        Func<SyntaxNode, string> rewrite)
    {
        return rewrite(whileStatement.Condition);
    }

    public static string CreateDoCondition(
        DoStatementSyntax doStatement,
        Func<SyntaxNode, string> rewrite)
    {
        return rewrite(doStatement.Condition);
    }
}
