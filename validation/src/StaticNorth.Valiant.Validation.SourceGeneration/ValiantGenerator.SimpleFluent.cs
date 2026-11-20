using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StaticNorth.Valiant.Validation.SourceGeneration;

public sealed partial class ValiantGenerator
{
    private static bool CanGenerateSimpleFluentValidator(
        INamedTypeSymbol validator,
        ITypeSymbol model,
        MethodDeclarationSyntax configure,
        SemanticModel semanticModel,
        ImmutableArray<ValidationNode>.Builder nodes,
        CancellationToken cancellationToken)
    {
        // A direct sealed override cannot dispatch to a different Configure implementation.
        if (!validator.IsSealed || model is not INamedTypeSymbol { TypeKind: TypeKind.Class } || configure.Body is not { Statements.Count: > 0 } body ||
            body.Statements.Count != nodes.Count ||
            semanticModel.GetDeclaredSymbol(configure, cancellationToken) is not IMethodSymbol { IsAsync: false, OverriddenMethod: { } overridden } ||
            !IsRuntimeType(overridden.ContainingType, "ValidatorDefinition`1") || configure.AttributeLists.Count != 0)
        {
            return false;
        }

        for (var index = 0; index < body.Statements.Count; index++)
        {
            // Inspect the original statements too. The metadata parser can omit statements and
            // accepts calls by name, which is insufficient when skipping user-authored Configure.
            if (body.Statements[index] is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation } ||
                !TryCollectChain(invocation, out var calls) ||
                calls[0].Arguments.Count != 1 ||
                calls[0].Arguments[0].Expression is not SimpleLambdaExpressionSyntax { Body: MemberAccessExpressionSyntax access } selector ||
                access.Expression is not IdentifierNameSyntax receiver ||
                receiver.Identifier.ValueText != selector.Parameter.Identifier.ValueText ||
                access.Name is not IdentifierNameSyntax member ||
                access.ToString() != receiver.Identifier.ValueText + "." + member.Identifier.ValueText ||
                semanticModel.GetSymbolInfo(access, cancellationToken).Symbol is not (IPropertySymbol { IsStatic: false } or IFieldSymbol { IsStatic: false }) ||
                semanticModel.GetSymbolInfo(calls[0].Invocation, cancellationToken).Symbol is not IMethodSymbol property ||
                !IsRuntimeType(property.ContainingType, "ValidationContext`1") ||
                property.TypeArguments.Length != 1 ||
                !SymbolEqualityComparer.Default.Equals(property.TypeArguments[0], semanticModel.GetTypeInfo(access, cancellationToken).Type))
            {
                return false;
            }

            if (nodes[index] is ChildPropertyValidationNode child)
            {
                if (!IsSimpleChildReuse(child, calls))
                {
                    return false;
                }

                continue;
            }

            if (nodes[index] is not RuleNode node || calls.Count != node.Rule.Checks.Length + 1)
            {
                return false;
            }

            for (var checkIndex = 0; checkIndex < node.Rule.Checks.Length; checkIndex++)
            {
                var call = calls[checkIndex + 1];
                var check = node.Rule.Checks[checkIndex];
                if (check.Code is not null || check.Message is not null || check.Severity is not null ||
                    call.Arguments.Count != check.Arguments.Length ||
                    call.Arguments.Any(static argument => argument.NameColon is not null) ||
                    semanticModel.GetSymbolInfo(call.Invocation, cancellationToken).Symbol is not IMethodSymbol method ||
                    !IsSimpleCheck(node.Rule, check, method.ReducedFrom ?? method))
                {
                    return false;
                }
            }
        }

        return true;

        bool IsSimpleChildReuse(ChildPropertyValidationNode child, List<CallInfo> calls)
        {
            // Read the dependency only when a child is present, as the deferred Children lambda does.
            return child.Nodes.Length == 1 && child.Nodes[0] is ValidatorReuseNode &&
                calls.Count == 2 && calls[1].Arguments.Count == 1 &&
                semanticModel.GetSymbolInfo(calls[1].Invocation, cancellationToken).Symbol is IMethodSymbol children &&
                IsRuntimeType((children.ReducedFrom ?? children).ContainingType, "ChildPropertyValidationExtensions") &&
                calls[1].Arguments[0].Expression is SimpleLambdaExpressionSyntax { Body: InvocationExpressionSyntax reuse } &&
                reuse.ArgumentList.Arguments.Count == 1 &&
                reuse.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax dependency &&
                semanticModel.GetSymbolInfo(reuse, cancellationToken).Symbol is IMethodSymbol useValidator &&
                IsRuntimeType(useValidator.ContainingType, "ValidationContext`1") &&
                semanticModel.GetSymbolInfo(dependency, cancellationToken).Symbol is IFieldSymbol { IsReadOnly: true, IsStatic: false } field &&
                SymbolEqualityComparer.Default.Equals(field.Type, useValidator.Parameters[0].Type);
        }

        bool IsRuntimeType(INamedTypeSymbol type, string name)
        {
            return SymbolEqualityComparer.Default.Equals(type.OriginalDefinition,
                semanticModel.Compilation.GetTypeByMetadataName("StaticNorth.Valiant.Validation." + name));
        }

        bool IsSimpleCheck(RuleModel rule, RuleCheck check, IMethodSymbol method)
        {
            if (check.Kind is "Required" or "NotEmpty" or "Empty")
            {
                return rule.IsString && IsRuntimeType(method.ContainingType, "PropertyValidation`2");
            }

            if (!IsRuntimeType(method.ContainingType, "TypedPropertyValidationExtensions"))
            {
                return false;
            }

            return check.Kind switch
            {
                "NotNull" or "Null" => true,
                "MinLength" or "MaxLength" or "LengthBetween" => rule.IsString && HasCultureInvariantIntegerLiteralArguments(check),
                "GreaterThan" or "GreaterThanOrEqualTo" or "LessThan" or "LessThanOrEqualTo" or "InclusiveBetween" or "ExclusiveBetween" =>
                    rule.PropertyType is "int" or "int?" && HasCultureInvariantIntegerLiteralArguments(check),
                _ => false
            };
        }
    }
}
