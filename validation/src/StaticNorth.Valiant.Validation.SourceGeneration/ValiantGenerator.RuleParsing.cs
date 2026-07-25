using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Valiant.Validation.SourceGeneration;

/// <summary>
/// Generates Valiant validation implementations and dependency injection registration code.
/// </summary>
public sealed partial class ValiantGenerator
{
    private static bool TryParseRule(
        InvocationExpressionSyntax invocation,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out RuleModel rule,
        out DiagnosticInfo? diagnostic)
    {
        rule = null!;
        diagnostic = null;

        if (!TryCollectChain(invocation, out var calls) ||
            calls.Count < 2 ||
            calls[0].Name != "Property" ||
            !IsIdentifier(calls[0].Receiver, scope.CurrentContextParameterName))
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                invocation.GetLocation(),
                "Property validations must start with a Property call on the validation context parameter.");
            return false;
        }

        if (calls[0].Arguments.Count != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                calls[0].Invocation.GetLocation(),
                "Property must have exactly one property selector argument.");
            return false;
        }

        if (!TryParsePropertySelector(calls[0].Arguments[0].Expression, semanticModel, cancellationToken, out var property, out diagnostic))
        {
            return false;
        }

        var checks = ImmutableArray.CreateBuilder<RuleCheck>();
        string? failureBehaviorExpression = null;
        string? conditionExpression = null;
        string? conditionDelegateType = null;
        var failureBehaviorCallCount = 0;
        for (var index = 1; index < calls.Count; index++)
        {
            var call = calls[index];
            if (TryGetFailureBehaviorExpression(call.Name, out var behaviorExpression))
            {
                failureBehaviorExpression = behaviorExpression;
                failureBehaviorCallCount++;
                continue;
            }

            if (call.Name == "When")
            {
                if (!TryGetWhenCondition(call, scope, semanticModel, cancellationToken, out var nextConditionExpression, out var nextConditionDelegateType, out diagnostic))
                {
                    return false;
                }

                CombineWhenCondition(nextConditionExpression, nextConditionDelegateType, ref conditionExpression, ref conditionDelegateType);
                continue;
            }

            if (!TryCreateCheck(call, scope, semanticModel, cancellationToken, out var check, out diagnostic))
            {
                return false;
            }

            if (!TryValidateCheckForProperty(property, call, check, out diagnostic))
            {
                return false;
            }

            checks.Add(check);
        }

        if (checks.Count == 0)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                invocation.GetLocation(),
                "Property must include at least one validator call.");
            return false;
        }

        rule = new RuleModel(
            property.PropertyName,
            GetPropertyNameExpression(scope, property.PropertyName),
            property.PropertyAccess,
            property.ParentPropertyAccesses,
            scope.CurrentInstanceExpression,
            property.PropertyType,
            property.MetadataPropertyType,
            property.IsString,
            property.IsInteger,
            property.IsNullableValueType,
            failureBehaviorExpression,
            failureBehaviorCallCount,
            conditionExpression,
            conditionDelegateType,
            invocation.GetLocation(),
            checks.ToImmutable());
        return true;
    }

    private static bool TryParseFailureBehavior(
        InvocationExpressionSyntax invocation,
        ParseScope scope,
        out FailureBehaviorNode node)
    {
        node = null!;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Name is not IdentifierNameSyntax identifier ||
            invocation.ArgumentList.Arguments.Count != 0 ||
            !IsIdentifier(memberAccess.Expression, scope.CurrentContextParameterName) ||
            !TryGetFailureBehaviorExpression(identifier.Identifier.ValueText, out var behaviorExpression))
        {
            return false;
        }

        node = new FailureBehaviorNode(behaviorExpression);
        return true;
    }

    private static bool TryParseBarePropertyDeclaration(
        InvocationExpressionSyntax invocation,
        ParseScope scope)
    {
        return TryCollectChain(invocation, out var calls) &&
            calls.Count == 1 &&
            calls[0].Name == "Property" &&
            IsIdentifier(calls[0].Receiver, scope.CurrentContextParameterName);
    }

    private static bool TryValidateCheckForProperty(
        PropertySelector property,
        CallInfo call,
        RuleCheck check,
        out DiagnosticInfo? diagnostic)
    {
        diagnostic = null;

        if (check.Kind == "EnumName" && !property.IsString)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "EnumName can only be used with string properties.");
            return false;
        }

        if (check.Kind == "EnumValue" && !property.IsInteger)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "EnumValue can only be used with integer properties.");
            return false;
        }

        return true;
    }

    private static bool TryParseValidatorReuse(
        InvocationExpressionSyntax invocation,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ValidatorReuseNode node,
        out DiagnosticInfo? diagnostic)
    {
        node = null!;
        diagnostic = null;

        _ = semanticModel;
        _ = cancellationToken;

        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax { Identifier.ValueText: "UseValidator" } } memberAccess ||
            !IsIdentifier(memberAccess.Expression, scope.CurrentContextParameterName))
        {
            return false;
        }

        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                invocation.GetLocation(),
                "UseValidator expects one injected validator expression.");
            return false;
        }

        node = new ValidatorReuseNode(
            RewriteForValidation(invocation.ArgumentList.Arguments[0].Expression, scope),
            scope.CurrentInstanceExpression,
            scope.PropertyPathPrefixExpression);
        return true;
    }

    private static bool TryParseItemRule(
        InvocationExpressionSyntax invocation,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out RuleModel rule,
        out DiagnosticInfo? diagnostic)
    {
        rule = null!;
        diagnostic = null;

        if (!TryCollectChain(invocation, out var calls) ||
            calls.Count < 3 ||
            calls[0].Name != "Item" ||
            calls[1].Name != "Property" ||
            !IsIdentifier(calls[0].Receiver, scope.CurrentContextParameterName))
        {
            return false;
        }

        if (calls[0].Arguments.Count != 3)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                calls[0].Invocation.GetLocation(),
                "Item expects item, collection selector, and index arguments.");
            return false;
        }

        if (calls[1].Arguments.Count != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                calls[1].Invocation.GetLocation(),
                "Property must have exactly one property selector argument.");
            return false;
        }

        if (!TryParsePropertySelector(calls[0].Arguments[1].Expression, semanticModel, cancellationToken, out var collectionProperty, out diagnostic) ||
            !TryParsePropertySelector(calls[1].Arguments[0].Expression, semanticModel, cancellationToken, out var itemProperty, out diagnostic))
        {
            return false;
        }

        var itemExpression = RewriteForValidation(calls[0].Arguments[0].Expression, scope);
        var indexExpression = RewriteForValidation(calls[0].Arguments[2].Expression, scope);
        var propertyNamePrefixExpression = GetCollectionItemPathPrefixExpression(scope, collectionProperty.PropertyName, indexExpression);

        var checks = ImmutableArray.CreateBuilder<RuleCheck>();
        string? failureBehaviorExpression = null;
        string? conditionExpression = null;
        string? conditionDelegateType = null;
        var failureBehaviorCallCount = 0;
        for (var index = 2; index < calls.Count; index++)
        {
            var call = calls[index];
            if (TryGetFailureBehaviorExpression(call.Name, out var behaviorExpression))
            {
                failureBehaviorExpression = behaviorExpression;
                failureBehaviorCallCount++;
                continue;
            }

            if (call.Name == "When")
            {
                if (!TryGetWhenCondition(call, scope, semanticModel, cancellationToken, out var nextConditionExpression, out var nextConditionDelegateType, out diagnostic))
                {
                    return false;
                }

                CombineWhenCondition(nextConditionExpression, nextConditionDelegateType, ref conditionExpression, ref conditionDelegateType);
                continue;
            }

            if (!TryCreateCheck(call, scope, semanticModel, cancellationToken, out var check, out diagnostic))
            {
                return false;
            }

            if (!TryValidateCheckForProperty(itemProperty, call, check, out diagnostic))
            {
                return false;
            }

            checks.Add(check);
        }

        if (checks.Count == 0)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                invocation.GetLocation(),
                "Item property must include at least one validator call.");
            return false;
        }

        rule = new RuleModel(
            itemProperty.PropertyName,
            propertyNamePrefixExpression + " + " + ToLiteral(itemProperty.PropertyName),
            itemProperty.PropertyAccess,
            itemProperty.ParentPropertyAccesses,
            itemExpression,
            itemProperty.PropertyType,
            itemProperty.MetadataPropertyType,
            itemProperty.IsString,
            itemProperty.IsInteger,
            itemProperty.IsNullableValueType,
            failureBehaviorExpression,
            failureBehaviorCallCount,
            conditionExpression,
            conditionDelegateType,
            invocation.GetLocation(),
            checks.ToImmutable());
        return true;
    }

    private static void CombineWhenCondition(
        string nextConditionExpression,
        string nextConditionDelegateType,
        ref string? conditionExpression,
        ref string? conditionDelegateType)
    {
        if (conditionExpression is null || conditionDelegateType is null)
        {
            conditionExpression = nextConditionExpression;
            conditionDelegateType = nextConditionDelegateType;
            return;
        }

        conditionExpression = "__valiantWhenInstance => ((" +
            conditionDelegateType +
            ")(" +
            conditionExpression +
            "))(__valiantWhenInstance) && ((" +
            nextConditionDelegateType +
            ")(" +
            nextConditionExpression +
            "))(__valiantWhenInstance)";
        conditionDelegateType = nextConditionDelegateType;
    }

    private static bool TryGetWhenCondition(
        CallInfo call,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string conditionExpression,
        out string conditionDelegateType,
        out DiagnosticInfo? diagnostic)
    {
        conditionExpression = string.Empty;
        conditionDelegateType = string.Empty;
        diagnostic = null;

        if (call.TypeArguments.Length != 0 || call.Arguments.Count != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "When expects one model predicate argument.");
            return false;
        }

        var method = semanticModel.GetSymbolInfo(call.Invocation, cancellationToken).Symbol as IMethodSymbol;
        if (method is null || method.Parameters.Length != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "Valiant could not determine the When predicate overload.");
            return false;
        }

        var delegateType = method.Parameters[0].Type as INamedTypeSymbol;
        var invokeMethod = delegateType?.DelegateInvokeMethod;
        if (delegateType is null || invokeMethod is null || invokeMethod.Parameters.Length != 1 || invokeMethod.ReturnType.SpecialType != SpecialType.System_Boolean)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "When supports model predicates that return bool.");
            return false;
        }

        conditionExpression = RewriteForValidation(call.Arguments[0].Expression, scope);
        conditionDelegateType = delegateType.ToDisplayString(TypeDisplayFormat);
        return true;
    }

    private static bool TryGetFailureBehaviorExpression(string callName, out string behaviorExpression)
    {
        switch (callName)
        {
            case "FailFast":
                behaviorExpression = "global::Valiant.Validation.ValiantFailureBehavior.FailFast";
                return true;
            case "ValidateAll":
                behaviorExpression = "global::Valiant.Validation.ValiantFailureBehavior.ValidateAll";
                return true;
            default:
                behaviorExpression = string.Empty;
                return false;
        }
    }

    private static bool TryParseCollectionIteration(
        InvocationExpressionSyntax invocation,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        out CollectionIterationNode node,
        out DiagnosticInfo? diagnostic)
    {
        node = null!;
        diagnostic = null;

        if (!TryCollectChain(invocation, out var calls) ||
            calls.Count != 2 ||
            calls[0].Name != "Property" ||
            calls[1].Name != "ForEach" ||
            !IsIdentifier(calls[0].Receiver, scope.CurrentContextParameterName))
        {
            return false;
        }

        if (calls[0].Arguments.Count != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                calls[0].Invocation.GetLocation(),
                "Property must have exactly one property selector argument.");
            return false;
        }

        if (!TryParsePropertySelector(calls[0].Arguments[0].Expression, semanticModel, cancellationToken, out var property, out diagnostic))
        {
            return false;
        }

        if (calls[1].Arguments.Count != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                calls[1].Invocation.GetLocation(),
                calls[1].Name + " expects one item validation lambda.");
            return false;
        }

        var lambda = calls[1].Arguments[0].Expression switch
        {
            SimpleLambdaExpressionSyntax simple => (LambdaExpressionSyntax)simple,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized,
            _ => null
        };

        if (lambda is null)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                calls[1].Arguments[0].GetLocation(),
                calls[1].Name + " expects an item validation lambda.");
            return false;
        }

        var itemContextParameterName = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized => parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 2 } parenthesized => parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
            _ => null
        };

        var collectionParameterName = lambda is ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 2 } twoParameterLambda
            ? twoParameterLambda.ParameterList.Parameters[1].Identifier.ValueText
            : null;

        if (itemContextParameterName is null)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                calls[1].Arguments[0].GetLocation(),
                calls[1].Name + " lambdas require one item context parameter or item context plus collection parameters.");
            return false;
        }

        var suffix = calls[1].Invocation.SpanStart.ToString(CultureInfo.InvariantCulture);
        var collectionLocalName = "__valiantCollection" + suffix;
        var itemLocalName = "__valiantItem" + suffix;
        var indexLocalName = "__valiantIndex" + suffix;
        var propertyPathPrefixExpression = GetCollectionItemPathPrefixExpression(scope, property.PropertyName, indexLocalName);
        var itemScope = scope.WithItemContext(
            itemContextParameterName,
            itemLocalName,
            collectionParameterName,
            collectionLocalName,
            propertyPathPrefixExpression);

        var parsedNodes = ImmutableArray.CreateBuilder<ValidationNode>();
        switch (lambda.Body)
        {
            case BlockSyntax block:
                ParseStatements(block.Statements, itemScope, semanticModel, cancellationToken, parsedNodes, diagnostics);
                break;
            case InvocationExpressionSyntax nestedInvocation:
                ParseStatement(SyntaxFactory.ExpressionStatement(nestedInvocation), itemScope, semanticModel, cancellationToken, parsedNodes, diagnostics);
                break;
            default:
                diagnostic = new DiagnosticInfo(
                    UnsupportedValidationDeclaration,
                    lambda.Body.GetLocation(),
                    calls[1].Name + " lambdas must contain validation declarations.");
                return false;
        }

        node = new CollectionIterationNode(
            scope.CurrentInstanceExpression + "." + property.PropertyAccess,
            scope.CurrentInstanceExpression,
            property.ParentPropertyAccesses,
            collectionLocalName,
            itemLocalName,
            indexLocalName,
            parsedNodes.ToImmutable());
        return true;
    }

    private static bool TryParseChildPropertyValidation(
        InvocationExpressionSyntax invocation,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        out ChildPropertyValidationNode node,
        out DiagnosticInfo? diagnostic)
    {
        node = null!;
        diagnostic = null;

        if (!TryCollectChain(invocation, out var calls) ||
            calls.Count != 2 ||
            calls[0].Name != "Property" ||
            calls[1].Name != "Children" ||
            !IsIdentifier(calls[0].Receiver, scope.CurrentContextParameterName))
        {
            return false;
        }

        if (calls[0].Arguments.Count != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                calls[0].Invocation.GetLocation(),
                "Property must have exactly one property selector argument.");
            return false;
        }

        if (!TryParsePropertySelector(calls[0].Arguments[0].Expression, semanticModel, cancellationToken, out var property, out diagnostic))
        {
            return false;
        }

        if (calls[1].Arguments.Count != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                calls[1].Invocation.GetLocation(),
                calls[1].Name + " expects one child validation lambda.");
            return false;
        }

        var lambda = calls[1].Arguments[0].Expression switch
        {
            SimpleLambdaExpressionSyntax simple => (LambdaExpressionSyntax)simple,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized,
            _ => null
        };

        if (lambda is null)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                calls[1].Arguments[0].GetLocation(),
                calls[1].Name + " expects a child validation lambda.");
            return false;
        }

        var childContextParameterName = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized => parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 2 } parenthesized => parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
            _ => null
        };

        var modelParameterName = lambda is ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 2 } twoParameterLambda
            ? twoParameterLambda.ParameterList.Parameters[1].Identifier.ValueText
            : null;

        if (childContextParameterName is null)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                calls[1].Arguments[0].GetLocation(),
                calls[1].Name + " lambdas require one child context parameter or child context plus whole model parameters.");
            return false;
        }

        var suffix = calls[1].Invocation.SpanStart.ToString(CultureInfo.InvariantCulture);
        var childLocalName = "__valiantChild" + suffix;
        var propertyPathPrefixExpression = GetChildPropertyPathPrefixExpression(scope, property.PropertyName);
        var childScope = scope.WithItemContext(
            childContextParameterName,
            childLocalName,
            modelParameterName,
            scope.CurrentInstanceExpression,
            propertyPathPrefixExpression);

        var parsedNodes = ImmutableArray.CreateBuilder<ValidationNode>();
        switch (lambda.Body)
        {
            case BlockSyntax block:
                ParseStatements(block.Statements, childScope, semanticModel, cancellationToken, parsedNodes, diagnostics);
                break;
            case InvocationExpressionSyntax nestedInvocation:
                ParseStatement(SyntaxFactory.ExpressionStatement(nestedInvocation), childScope, semanticModel, cancellationToken, parsedNodes, diagnostics);
                break;
            default:
                diagnostic = new DiagnosticInfo(
                    UnsupportedValidationDeclaration,
                    lambda.Body.GetLocation(),
                    calls[1].Name + " lambdas must contain validation declarations.");
                return false;
        }

        node = new ChildPropertyValidationNode(
            scope.CurrentInstanceExpression + "." + property.PropertyAccess,
            scope.CurrentInstanceExpression,
            property.ParentPropertyAccesses,
            childLocalName,
            parsedNodes.ToImmutable());
        return true;
    }

    private static bool TryCreateCheck(
        CallInfo call,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out RuleCheck check,
        out DiagnosticInfo? diagnostic)
    {
        check = null!;
        diagnostic = null;

        if (call.Name is "Satisfies" or "SatisfiesAsync")
        {
            return TryCreateCustomCheck(call, scope, semanticModel, cancellationToken, out check, out diagnostic);
        }

        if (call.Name == "UseValidator")
        {
            return TryCreatePropertyValidatorReuseCheck(call, scope, out check, out diagnostic);
        }

        if (call.Name is "All" or "Any" or "None")
        {
            return TryCreateCollectionPredicateCheck(call, scope, semanticModel, cancellationToken, out check, out diagnostic);
        }

        if (call.Name is "EnumName" or "EnumValue")
        {
            return TryCreateEnumCheck(call, scope, semanticModel, cancellationToken, out check, out diagnostic);
        }

        var expectedTypeArguments = GetExpectedTypeArgumentCount(call.Name);
        if (expectedTypeArguments < 0)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "Validator call '" + call.Name + "' is not supported yet.");
            return false;
        }

        var expectedArguments = GetExpectedArgumentCount(call.Name);
        if (expectedArguments < 0)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "Validator call '" + call.Name + "' is not supported yet.");
            return false;
        }

        if (!TrySplitCheckArguments(
            call,
            expectedArguments,
            scope,
            semanticModel,
            cancellationToken,
            out var arguments,
            out var code,
            out var message,
            out var severity,
            out diagnostic))
        {
            return false;
        }

        if (!TryGetTypeArguments(call, expectedTypeArguments, semanticModel, cancellationToken, out var typeArguments, out diagnostic))
        {
            return false;
        }

        check = new RuleCheck(call.Name, arguments, typeArguments, false, null, 0, code, message, severity, call.Invocation.GetLocation());
        return true;
    }

    private static bool TryCreatePropertyValidatorReuseCheck(
        CallInfo call,
        ParseScope scope,
        out RuleCheck check,
        out DiagnosticInfo? diagnostic)
    {
        check = null!;
        diagnostic = null;

        if (call.TypeArguments.Length != 0 || call.Arguments.Count != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "UseValidator expects one injected property validator expression.");
            return false;
        }

        check = new RuleCheck(
            "UseValidator",
            [RewriteForValidation(call.Arguments[0].Expression, scope)],
            ImmutableArray<string>.Empty,
            false,
            null,
            0,
            null,
            null,
            null,
            call.Invocation.GetLocation());
        return true;
    }

    private static bool TryCreateEnumCheck(
        CallInfo call,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out RuleCheck check,
        out DiagnosticInfo? diagnostic)
    {
        check = null!;
        diagnostic = null;

        var hasGenericEnumType = call.TypeArguments.Length == 1;
        if (hasGenericEnumType)
        {
            if (!TrySplitCheckArguments(
                call,
                0,
            scope,
            semanticModel,
            cancellationToken,
                out _,
                out var code,
                out var message,
                out var severity,
            out diagnostic))
            {
                return false;
            }

            if (!TryGetTypeArguments(call, 1, semanticModel, cancellationToken, out var typeArguments, out diagnostic))
            {
                return false;
            }

            check = new RuleCheck(
                call.Name,
                ["typeof(" + typeArguments[0] + ")"],
                typeArguments,
                false,
                null,
                0,
                code,
                message,
                severity,
                call.Invocation.GetLocation());
            return true;
        }

        if (call.TypeArguments.Length != 0)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "Validator call '" + call.Name + "' expects one enum type argument or one typeof enum argument.");
            return false;
        }

        if (!TrySplitCheckArguments(
            call,
            1,
            scope,
            semanticModel,
            cancellationToken,
            out var arguments,
            out var typeofCode,
            out var typeofMessage,
            out var typeofSeverity,
            out diagnostic))
        {
            return false;
        }

        check = new RuleCheck(call.Name, arguments, ImmutableArray<string>.Empty, false, null, 0, typeofCode, typeofMessage, typeofSeverity, call.Invocation.GetLocation());
        return true;
    }

    private static bool TryGetTypeArguments(
        CallInfo call,
        int expectedTypeArgumentCount,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ImmutableArray<string> typeArguments,
        out DiagnosticInfo? diagnostic)
    {
        typeArguments = ImmutableArray<string>.Empty;
        diagnostic = null;

        if (call.TypeArguments.Length != expectedTypeArgumentCount)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "Validator call '" + call.Name + "' expects " + expectedTypeArgumentCount.ToString(CultureInfo.InvariantCulture) + " type argument(s).");
            return false;
        }

        if (expectedTypeArgumentCount == 0)
        {
            return true;
        }

        var builder = ImmutableArray.CreateBuilder<string>(expectedTypeArgumentCount);
        foreach (var typeArgument in call.TypeArguments)
        {
            var type = semanticModel.GetTypeInfo(typeArgument, cancellationToken).Type;
            if (type is null)
            {
                diagnostic = new DiagnosticInfo(
                    UnsupportedValidationDeclaration,
                    typeArgument.GetLocation(),
                    "Valiant could not determine the validator call type argument.");
                return false;
            }

            builder.Add(type.ToDisplayString(TypeDisplayFormat));
        }

        typeArguments = builder.ToImmutable();
        return true;
    }

    private static bool TrySplitCheckArguments(
        CallInfo call,
        int requiredArgumentCount,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ImmutableArray<string> validationArguments,
        out string? code,
        out string? message,
        out string? severity,
        out DiagnosticInfo? diagnostic)
    {
        validationArguments = ImmutableArray<string>.Empty;
        code = null;
        message = null;
        severity = null;
        diagnostic = null;

        _ = semanticModel;
        _ = cancellationToken;

        var arguments = ImmutableArray.CreateBuilder<ArgumentSyntax>();
        foreach (var argument in call.Arguments)
        {
            var argumentName = argument.NameColon?.Name.Identifier.ValueText;
            switch (argumentName)
            {
                case "code":
                    if (code is not null)
                    {
                        diagnostic = new DiagnosticInfo(
                            UnsupportedValidationDeclaration,
                            argument.GetLocation(),
                            "Validator call '" + call.Name + "' specifies code more than once.");
                        return false;
                    }

                    code = RewriteForValidation(argument.Expression, scope);
                    break;

                case "message":
                    if (message is not null)
                    {
                        diagnostic = new DiagnosticInfo(
                            UnsupportedValidationDeclaration,
                            argument.GetLocation(),
                            "Validator call '" + call.Name + "' specifies message more than once.");
                        return false;
                    }

                    message = RewriteForValidation(argument.Expression, scope);
                    break;

                case "severity":
                    if (severity is not null)
                    {
                        diagnostic = new DiagnosticInfo(
                            UnsupportedValidationDeclaration,
                            argument.GetLocation(),
                            "Validator call '" + call.Name + "' specifies severity more than once.");
                        return false;
                    }

                    severity = RewriteForValidation(argument.Expression, scope);
                    break;

                default:
                    arguments.Add(argument);
                    break;
            }
        }

        if (arguments.Count > requiredArgumentCount + 3)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "Validator call '" + call.Name + "' expects " + requiredArgumentCount.ToString(CultureInfo.InvariantCulture) + " validation argument(s), with optional code, message, and severity arguments.");
            return false;
        }

        if (arguments.Count > requiredArgumentCount)
        {
            if (code is not null)
            {
                diagnostic = new DiagnosticInfo(
                    UnsupportedValidationDeclaration,
                    arguments[requiredArgumentCount].GetLocation(),
                    "Validator call '" + call.Name + "' specifies code more than once.");
                return false;
            }

            code = RewriteForValidation(arguments[requiredArgumentCount].Expression, scope);
        }

        if (arguments.Count > requiredArgumentCount + 1)
        {
            if (message is not null)
            {
                diagnostic = new DiagnosticInfo(
                    UnsupportedValidationDeclaration,
                    arguments[requiredArgumentCount + 1].GetLocation(),
                    "Validator call '" + call.Name + "' specifies message more than once.");
                return false;
            }

            message = RewriteForValidation(arguments[requiredArgumentCount + 1].Expression, scope);
        }

        if (arguments.Count > requiredArgumentCount + 2)
        {
            if (severity is not null)
            {
                diagnostic = new DiagnosticInfo(
                    UnsupportedValidationDeclaration,
                    arguments[requiredArgumentCount + 2].GetLocation(),
                    "Validator call '" + call.Name + "' specifies severity more than once.");
                return false;
            }

            severity = RewriteForValidation(arguments[requiredArgumentCount + 2].Expression, scope);
        }

        if (arguments.Count < requiredArgumentCount)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "Validator call '" + call.Name + "' expects " + requiredArgumentCount.ToString(CultureInfo.InvariantCulture) + " validation argument(s), with optional code, message, and severity arguments.");
            return false;
        }

        var validationArgumentBuilder = ImmutableArray.CreateBuilder<string>(requiredArgumentCount);
        for (var index = 0; index < requiredArgumentCount; index++)
        {
            validationArgumentBuilder.Add(RewriteForValidation(arguments[index].Expression, scope));
        }

        validationArguments = validationArgumentBuilder.ToImmutable();
        return true;
    }

    private static bool TryCreateCustomCheck(
        CallInfo call,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out RuleCheck check,
        out DiagnosticInfo? diagnostic)
    {
        check = null!;
        diagnostic = null;

        if (!TrySplitCheckArguments(
            call,
            1,
            scope,
            semanticModel,
            cancellationToken,
            out var arguments,
            out var code,
            out var message,
            out var severity,
            out diagnostic))
        {
            return false;
        }

        var method = semanticModel.GetSymbolInfo(call.Invocation, cancellationToken).Symbol as IMethodSymbol;
        if (method is null || method.Parameters.Length == 0)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "Valiant could not determine the custom validation predicate overload.");
            return false;
        }

        var delegateType = method.Parameters[0].Type as INamedTypeSymbol;
        var invokeMethod = delegateType?.DelegateInvokeMethod;
        if (delegateType is null || invokeMethod is null)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "Custom validation predicates must be delegate-compatible lambdas or method groups.");
            return false;
        }

        var parameterCount = invokeMethod.Parameters.Length;
        if (call.Name == "Satisfies" && parameterCount is not 1 and not 2)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "Satisfies supports property-only predicates or model-and-property predicates.");
            return false;
        }

        if (call.Name == "SatisfiesAsync" && parameterCount is not 2 and not 3)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "SatisfiesAsync supports property-and-cancellation-token predicates or model-property-and-cancellation-token predicates.");
            return false;
        }

        check = new RuleCheck(
            call.Name,
            arguments,
            ImmutableArray<string>.Empty,
            call.Name == "SatisfiesAsync",
            delegateType.ToDisplayString(TypeDisplayFormat),
            parameterCount,
            code,
            message,
            severity,
            call.Invocation.GetLocation());
        return true;
    }

    private static bool TryCreateCollectionPredicateCheck(
        CallInfo call,
        ParseScope scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out RuleCheck check,
        out DiagnosticInfo? diagnostic)
    {
        check = null!;
        diagnostic = null;

        if (!TrySplitCheckArguments(
            call,
            1,
            scope,
            semanticModel,
            cancellationToken,
            out var arguments,
            out var code,
            out var message,
            out var severity,
            out diagnostic))
        {
            return false;
        }

        var method = semanticModel.GetSymbolInfo(call.Invocation, cancellationToken).Symbol as IMethodSymbol;
        if (method is null || method.Parameters.Length == 0)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                "Valiant could not determine the collection predicate overload.");
            return false;
        }

        var delegateType = method.Parameters[0].Type as INamedTypeSymbol;
        var invokeMethod = delegateType?.DelegateInvokeMethod;
        if (delegateType is null || invokeMethod is null || invokeMethod.Parameters.Length != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                call.Invocation.GetLocation(),
                call.Name + " supports item-only predicates.");
            return false;
        }

        check = new RuleCheck(
            call.Name,
            arguments,
            ImmutableArray<string>.Empty,
            false,
            delegateType.ToDisplayString(TypeDisplayFormat),
            1,
            code,
            message,
            severity,
            call.Invocation.GetLocation());
        return true;
    }

    private static int GetExpectedTypeArgumentCount(string callName)
    {
        switch (callName)
        {
            case "NotNull":
            case "Null":
            case "Required":
            case "NotEmpty":
            case "Empty":
            case "DefinedEnum":
            case "EnumName":
            case "EnumValue":
            case "LengthBetween":
            case "MinLength":
            case "MaxLength":
            case "GreaterThan":
            case "GreaterThanOrEqualTo":
            case "LessThan":
            case "LessThanOrEqualTo":
            case "Equal":
            case "NotEqual":
            case "InclusiveBetween":
            case "ExclusiveBetween":
            case "Count":
            case "CountBetween":
            case "MinCount":
            case "MaxCount":
            case "Contains":
            case "Excludes":
            case "HasKey":
            case "HasValue":
                return 0;
            default:
                return -1;
        }
    }

    private static int GetExpectedArgumentCount(string callName)
    {
        switch (callName)
        {
            case "NotNull":
            case "Null":
            case "Required":
            case "NotEmpty":
            case "Empty":
            case "DefinedEnum":
                return 0;
            case "EnumName":
            case "EnumValue":
            case "MinLength":
            case "MaxLength":
            case "MinCount":
            case "MaxCount":
            case "Count":
            case "GreaterThan":
            case "GreaterThanOrEqualTo":
            case "LessThan":
            case "LessThanOrEqualTo":
            case "Equal":
            case "NotEqual":
            case "Contains":
            case "Excludes":
            case "HasKey":
            case "HasValue":
                return 1;
            case "LengthBetween":
            case "InclusiveBetween":
            case "ExclusiveBetween":
            case "CountBetween":
                return 2;
            default:
                return -1;
        }
    }

    private static string GetPropertyNameExpression(ParseScope scope, string propertyName)
    {
        return scope.PropertyPathPrefixExpression is null
            ? ToLiteral(propertyName)
            : scope.PropertyPathPrefixExpression + " + " + ToLiteral(propertyName);
    }

    private static string GetCollectionItemPathPrefixExpression(ParseScope scope, string propertyName, string indexName)
    {
        var itemPrefix = ToLiteral(propertyName + "[") + " + (" + indexName + ").ToString(global::System.Globalization.CultureInfo.InvariantCulture) + " + ToLiteral("].");
        return scope.PropertyPathPrefixExpression is null
            ? itemPrefix
            : scope.PropertyPathPrefixExpression + " + " + itemPrefix;
    }

    private static string GetChildPropertyPathPrefixExpression(ParseScope scope, string propertyName)
    {
        var childPrefix = ToLiteral(propertyName + ".");
        return scope.PropertyPathPrefixExpression is null
            ? childPrefix
            : scope.PropertyPathPrefixExpression + " + " + childPrefix;
    }

    private static bool TryParsePropertySelector(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out PropertySelector selector,
        out DiagnosticInfo? diagnostic)
    {
        selector = null!;
        diagnostic = null;

        var lambda = expression switch
        {
            SimpleLambdaExpressionSyntax simple => (LambdaExpressionSyntax)simple,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized,
            _ => null
        };

        if (lambda?.Body is not ExpressionSyntax body)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                expression.GetLocation(),
                "Property currently requires a simple property lambda such as x => x.Name.");
            return false;
        }

        var parameterName = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized => parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
            _ => null
        };

        if (parameterName is null)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                expression.GetLocation(),
                "Property currently requires exactly one lambda parameter.");
            return false;
        }

        body = UnwrapExpression(body);

        var accessSegments = new Stack<string>();
        var displaySegments = new Stack<string>();
        var current = body;

        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            accessSegments.Push(memberAccess.Name.ToString());
            displaySegments.Push(memberAccess.Name.Identifier.ValueText);
            current = UnwrapExpression(memberAccess.Expression);
        }

        if (!IsIdentifier(current, parameterName) || accessSegments.Count == 0)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                expression.GetLocation(),
                "Property currently supports direct property access, for example x => x.Name or x => x.Address.City.");
            return false;
        }

        var type = semanticModel.GetTypeInfo(body, cancellationToken).Type;
        if (type is null || type is IErrorTypeSymbol)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                expression.GetLocation(),
                "Valiant could not determine the selected property type.");
            return false;
        }

        var propertyAccesses = accessSegments.ToArray();
        var parentPropertyAccesses = ImmutableArray.CreateBuilder<string>();
        for (var index = 0; index < propertyAccesses.Length - 1; index++)
        {
            parentPropertyAccesses.Add(string.Join(".", propertyAccesses.Take(index + 1)));
        }

        selector = new PropertySelector(
            string.Join(".", displaySegments),
            string.Join(".", propertyAccesses),
            parentPropertyAccesses.ToImmutable(),
            type.ToDisplayString(TypeDisplayFormat),
            type.ToDisplayString(MetadataTypeDisplayFormat),
            type.SpecialType == SpecialType.System_String,
            IsIntegerType(type),
            IsNullableValueType(type));
        return true;
    }
}
