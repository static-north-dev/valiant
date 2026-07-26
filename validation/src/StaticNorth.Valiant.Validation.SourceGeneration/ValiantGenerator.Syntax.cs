using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StaticNorth.Valiant.SourceGeneration;
using static StaticNorth.Valiant.SourceGeneration.ValiantSourceBuilder;
using static StaticNorth.Valiant.SourceGeneration.ValiantTypeDeclarations;

namespace StaticNorth.Valiant.Validation.SourceGeneration;

/// <summary>
/// Generates Valiant validation implementations and dependency injection registration code.
/// </summary>
public sealed partial class ValiantGenerator
{
    private static bool IsIntegerType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 } namedType)
        {
            type = namedType.TypeArguments[0];
        }

        return type.SpecialType is SpecialType.System_Byte or
            SpecialType.System_SByte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64;
    }

    private static bool IsNullableValueType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 };
    }

    private static ExpressionSyntax UnwrapExpression(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    expression = postfix.Operand;
                    continue;
                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    continue;
                default:
                    return expression;
            }
        }
    }

    private static string RewriteForValidation(SyntaxNode node, string contextParameterName)
    {
        return RewriteForValidation(node, ParseScope.Root(contextParameterName));
    }

    private static string RewriteForValidation(SyntaxNode node, ParseScope scope)
    {
        var rewritten = new ValidationContextRewriter(scope).Visit(node);
        return rewritten?.ToString() ?? node.ToString();
    }

    private static bool TryCollectChain(InvocationExpressionSyntax invocation, out List<CallInfo> calls)
    {
        calls = [];
        ExpressionSyntax current = invocation;

        while (current is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } currentInvocation)
        {
            var name = GetInvocationName(memberAccess.Name, out var typeArguments);
            if (name is null)
            {
                calls = [];
                return false;
            }

            calls.Add(new CallInfo(
                name,
                memberAccess.Expression,
                typeArguments,
                currentInvocation.ArgumentList.Arguments,
                currentInvocation));

            current = memberAccess.Expression;
        }

        calls.Reverse();
        return calls.Count > 0;
    }

    private static string? GetInvocationName(SimpleNameSyntax nameSyntax, out ImmutableArray<TypeSyntax> typeArguments)
    {
        switch (nameSyntax)
        {
            case IdentifierNameSyntax identifier:
                typeArguments = ImmutableArray<TypeSyntax>.Empty;
                return identifier.Identifier.ValueText;
            case GenericNameSyntax generic:
                typeArguments = [..generic.TypeArgumentList.Arguments];
                return generic.Identifier.ValueText;
            default:
                typeArguments = ImmutableArray<TypeSyntax>.Empty;
                return null;
        }
    }

    private static bool IsIdentifier(ExpressionSyntax expression, string name)
    {
        return expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == name;
    }

    private static bool IsSupportedRawExpression(ExpressionSyntax expression)
    {
        return expression is AssignmentExpressionSyntax ||
            expression is PrefixUnaryExpressionSyntax prefix && (prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression)) ||
            expression is PostfixUnaryExpressionSyntax postfix && (postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression));
    }

    private static MethodDeclarationSyntax? FindConfigureMethod(INamedTypeSymbol validatorSymbol, string configureMethodName, CancellationToken cancellationToken)
    {
        foreach (var reference in validatorSymbol.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reference.GetSyntax(cancellationToken) is not ClassDeclarationSyntax classDeclaration)
            {
                continue;
            }

            foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
            {
                if (method.Identifier.ValueText == configureMethodName)
                {
                    return method;
                }
            }
        }

        return null;
    }

    private static string GetConfigureMethodName(AttributeData attribute)
    {
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (namedArgument is { Key: "ConfigureMethod", Value.Value: string methodName } &&
                !string.IsNullOrWhiteSpace(methodName))
            {
                return methodName;
            }
        }

        return "Configure";
    }

    private static ImmutableArray<string> CollectUsings(ClassDeclarationSyntax classSyntax)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (classSyntax.SyntaxTree.GetCompilationUnitRoot() is CompilationUnitSyntax compilationUnit)
        {
            AddUsings(compilationUnit.Usings, builder, seen);
        }

        for (SyntaxNode? current = classSyntax.Parent; current is not null; current = current.Parent)
        {
            if (current is BaseNamespaceDeclarationSyntax namespaceDeclaration)
            {
                AddUsings(namespaceDeclaration.Usings, builder, seen);
            }
        }

        return builder.ToImmutable();
    }

    private static void AddUsings(
        SyntaxList<UsingDirectiveSyntax> usings,
        ImmutableArray<string>.Builder builder,
        HashSet<string> seen)
    {
        foreach (var usingDirective in usings)
        {
            var usingText = usingDirective.NormalizeWhitespace().ToFullString();
            if (seen.Add(usingText))
            {
                builder.Add(usingText);
            }
        }
    }

    private static string GetHintName(INamedTypeSymbol symbol, string suffix)
    {
        return ValiantHintNames.Create(symbol, suffix);
    }

    private static string ToLiteral(string value)
    {
        return ValiantSourceLiterals.String(value);
    }
}
