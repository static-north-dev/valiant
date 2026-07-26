using System.Collections.Immutable;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using StaticNorth.Valiant.SourceGeneration;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.SourceGeneration;

/// <summary>
/// Generates ASP.NET Core endpoint route registration code for Valiant endpoints.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ValiantEndpointGenerator : IIncrementalGenerator
{
    private const string EndpointAttributeMetadataName = "StaticNorth.Valiant.Endpoints.ValiantEndpointAttribute";
    private const string GenericEndpointAttributeMetadataName = "StaticNorth.Valiant.Endpoints.ValiantEndpointAttribute`1";
    private const string EndpointGroupAttributeMetadataName = "StaticNorth.Valiant.Endpoints.ValiantEndpointGroupAttribute";
    private const string EndpointServiceCollectionExtensionsMetadataName = "StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection.ValiantEndpointServiceCollectionExtensions";
    private const string QueryParameterOptionsMetadataName = "StaticNorth.Valiant.Endpoints.AspNetCore.QueryParameterOptions";
    private const string QueryParsingAttributeMetadataName = "StaticNorth.Valiant.Endpoints.AspNetCore.ValiantQueryParsingAttribute";
    private const string FromQueryAttributeMetadataName = "Microsoft.AspNetCore.Mvc.FromQueryAttribute";
    private const string AsParametersAttributeMetadataName = "Microsoft.AspNetCore.Http.AsParametersAttribute";
    private const string AspNetCoreNamespace = "StaticNorth.Valiant.Endpoints.AspNetCore";
    private const string EndpointMetadataName = "IEndpoint";
    private const string EndpointGroupMetadataName = "IEndpointGroup";

    private static readonly DiagnosticDescriptor UnsupportedEndpointShape = new(
        "VLE001",
        "Unsupported endpoint shape",
        "Endpoint declaration '{0}' must implement StaticNorth.Valiant.Endpoints.AspNetCore.IEndpoint",
        "StaticNorth.Valiant.Endpoints",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor AmbiguousEndpointShape = new(
        "VLE002",
        "Ambiguous endpoint shape",
        "Endpoint declaration '{0}' must not implement both IEndpoint and IEndpointGroup",
        "StaticNorth.Valiant.Endpoints",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor EndpointAttributeCannotMarkGroup = new(
        "VLE003",
        "Endpoint group must use group attribute",
        "Endpoint group '{0}' must use ValiantEndpointGroupAttribute instead of ValiantEndpointAttribute",
        "StaticNorth.Valiant.Endpoints",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidEndpointGroupType = new(
        "VLE004",
        "Invalid endpoint group type",
        "Endpoint '{0}' declares group type '{1}', but that type does not implement StaticNorth.Valiant.Endpoints.AspNetCore.IEndpointGroup",
        "StaticNorth.Valiant.Endpoints",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedEndpointGroupShape = new(
        "VLE005",
        "Unsupported endpoint group shape",
        "Endpoint group declaration '{0}' must implement StaticNorth.Valiant.Endpoints.AspNetCore.IEndpointGroup",
        "StaticNorth.Valiant.Endpoints",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor MissingEndpointAttribute = new(
        "VLE006",
        "Missing endpoint attribute",
        "Endpoint declaration '{0}' must be marked with ValiantEndpointAttribute",
        "StaticNorth.Valiant.Endpoints",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor MissingEndpointGroupAttribute = new(
        "VLE007",
        "Missing endpoint group attribute",
        "Endpoint group declaration '{0}' must be marked with ValiantEndpointGroupAttribute",
        "StaticNorth.Valiant.Endpoints",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor EndpointGroupAttributeCannotMarkEndpoint = new(
        "VLE008",
        "Endpoint must use endpoint attribute",
        "Endpoint declaration '{0}' must use ValiantEndpointAttribute instead of ValiantEndpointGroupAttribute",
        "StaticNorth.Valiant.Endpoints",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateEndpointAttribute = new(
        "VLE009",
        "Duplicate endpoint attribute",
        "Endpoint declaration '{0}' must use only one ValiantEndpointAttribute form",
        "StaticNorth.Valiant.Endpoints",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedQueryObjectBindingShape = new(
        "VLE010",
        "Unsupported query object binding shape",
        "Query object type '{0}' cannot receive a generated query binder because {1}",
        "StaticNorth.Valiant.Endpoints",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ConflictingQueryJsonPolicies = new(
        "VLE011",
        "Conflicting query JSON policies",
        "Query object type '{0}' is used by direct query parameters with conflicting JsonValues policies",
        "StaticNorth.Valiant.Endpoints",
        DiagnosticSeverity.Error,
        true);

    private static readonly SymbolDisplayFormat TypeDisplayFormat = ValiantGeneratorTypeDisplayFormats.TypeDisplayFormat;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var queryBindingOptIns = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsPotentialQueryBindingOptIn(node),
                static (context, cancellationToken) => IsQueryBindingOptIn(context, cancellationToken));
        var queryBindingEnabled = context.ParseOptionsProvider
            .Combine(queryBindingOptIns.Collect())
            .Select(static (configuration, _) => configuration.Right.Any(static enabled => enabled))
            .WithTrackingName("Endpoints.QueryBindingEnabled");

        var endpoints = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                EndpointAttributeMetadataName,
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (context, cancellationToken) => CreateEndpointCandidate(context, cancellationToken));

        var genericEndpoints = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenericEndpointAttributeMetadataName,
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (context, cancellationToken) => CreateEndpointCandidate(context, cancellationToken));

        var endpointGroups = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                EndpointGroupAttributeMetadataName,
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (context, _) => CreateEndpointGroupCandidate(context));

        var validEndpoints = endpoints
            .Select(static (candidate, _) => candidate.Endpoint)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .WithComparer(EndpointModelComparer.Instance)
            .WithTrackingName("Endpoints.Endpoint");

        var validGenericEndpoints = genericEndpoints
            .Select(static (candidate, _) => candidate.Endpoint)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .WithComparer(EndpointModelComparer.Instance)
            .WithTrackingName("Endpoints.GenericEndpoint");

        var validEndpointGroups = endpointGroups
            .Select(static (candidate, _) => candidate.Endpoint)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .WithComparer(EndpointModelComparer.Instance)
            .WithTrackingName("Endpoints.Group");

        var hasDiagnostics = endpoints
            .Collect()
            .Combine(genericEndpoints.Collect())
            .Combine(endpointGroups.Collect())
            .Combine(queryBindingEnabled)
            .Select(static (candidates, _) => HasDiagnostics(candidates.Left.Left.Left, candidates.Right) ||
                HasDiagnostics(candidates.Left.Left.Right, candidates.Right) ||
                HasDiagnostics(candidates.Left.Right, includeQueryBindingDiagnostics: false));

        var registrations = validEndpoints
            .Collect()
            .Combine(validGenericEndpoints.Collect())
            .Combine(validEndpointGroups.Collect())
            .Combine(hasDiagnostics.Combine(queryBindingEnabled))
            .Select(static (models, _) => models.Right.Left
                ? new EndpointRegistrationModel(
                    ImmutableArray<EndpointModel>.Empty,
                    queryBindingGenerated: false)
                : CreateEndpointRegistrationModel(
                    models.Left.Left.Left,
                    models.Left.Left.Right,
                    models.Left.Right,
                    models.Right.Right))
            .WithComparer(EndpointRegistrationModelComparer.Instance)
            .WithTrackingName("Endpoints.Registrations");

        var queryObjectBindings = endpoints
            .Select(static (candidate, _) => new QueryObjectBindingRegistrationModel(candidate.QueryObjectBindings))
            .WithComparer(QueryObjectBindingRegistrationModelComparer.Instance);

        var genericQueryObjectBindings = genericEndpoints
            .Select(static (candidate, _) => new QueryObjectBindingRegistrationModel(candidate.QueryObjectBindings))
            .WithComparer(QueryObjectBindingRegistrationModelComparer.Instance);

        var queryObjectBindingRegistrations = queryObjectBindings
            .Collect()
            .Combine(genericQueryObjectBindings.Collect())
            .Combine(queryBindingEnabled)
            .Select(static (models, _) => models.Right
                ? CreateQueryObjectBindingRegistrationModel(models.Left.Left, models.Left.Right)
                : new QueryObjectBindingRegistrationModel(ImmutableArray<QueryObjectBindingModel>.Empty))
            .WithComparer(QueryObjectBindingRegistrationModelComparer.Instance)
            .WithTrackingName("Endpoints.QueryBindings");

        context.RegisterSourceOutput(
            endpoints.Combine(queryBindingEnabled),
            static (context, candidate) => ExecuteDiagnostics(context, candidate.Left, candidate.Right));

        context.RegisterSourceOutput(
            genericEndpoints.Combine(queryBindingEnabled),
            static (context, candidate) => ExecuteDiagnostics(context, candidate.Left, candidate.Right));

        context.RegisterSourceOutput(
            endpointGroups,
            static (context, candidate) => ExecuteDiagnostics(context, candidate));

        context.RegisterSourceOutput(
            registrations,
            static (context, registration) => Execute(context, registration));

        context.RegisterSourceOutput(
            queryObjectBindingRegistrations,
            static (context, registration) => ExecuteQueryObjectBindings(context, registration));
    }

    private static bool IsPotentialQueryBindingOptIn(SyntaxNode node)
    {
        return node is AssignmentExpressionSyntax
        {
            RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
            Left: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "EnableBinding" },
            Right: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.TrueLiteralExpression }
        };
    }

    private static bool IsQueryBindingOptIn(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.Node is not AssignmentExpressionSyntax assignment ||
            context.SemanticModel.GetOperation(assignment, cancellationToken) is not ISimpleAssignmentOperation
            {
                Target: IPropertyReferenceOperation propertyReference,
                Value.ConstantValue: { HasValue: true, Value: true }
            } ||
            propertyReference.Property.Name != "EnableBinding" ||
            propertyReference.Property.ContainingType.ToDisplayString() != QueryParameterOptionsMetadataName)
        {
            return false;
        }

        LambdaExpressionSyntax? setupLambda = assignment.Parent switch
        {
            LambdaExpressionSyntax expressionLambda => expressionLambda,
            ExpressionStatementSyntax { Parent: BlockSyntax { Parent: LambdaExpressionSyntax blockLambda } } => blockLambda,
            _ => null
        };
        if (setupLambda?.Parent is not ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax invocation })
        {
            return false;
        }

        if (context.SemanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation invocationOperation)
        {
            return false;
        }

        var method = invocationOperation.TargetMethod.ReducedFrom ?? invocationOperation.TargetMethod;
        return method.Name == "AddValiantEndpoints" &&
            method.ContainingType.ToDisplayString() == EndpointServiceCollectionExtensionsMetadataName;
    }

    private static EndpointCandidateModel CreateEndpointCandidate(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return new EndpointCandidateModel(null, ImmutableArray<ValiantDiagnosticInfo>.Empty);
        }

        var isGroup = ValiantSymbolMatching.ImplementsInterface(typeSymbol, AspNetCoreNamespace, EndpointGroupMetadataName);
        var isEndpoint = ValiantSymbolMatching.ImplementsInterface(typeSymbol, AspNetCoreNamespace, EndpointMetadataName);
        var typeName = typeSymbol.ToDisplayString(TypeDisplayFormat);
        var groupTypeSymbol = GetEndpointGroupTypeSymbol(context.Attributes);
        var order = GetEndpointOrder(context.Attributes);
        var location = typeSymbol.Locations.FirstOrDefault();

        if (CountEndpointAttributes(typeSymbol) > 1)
        {
            return new EndpointCandidateModel(
                null,
                ImmutableArray.Create(new ValiantDiagnosticInfo(DuplicateEndpointAttribute, location, typeName)));
        }

        if (!isGroup && !isEndpoint)
        {
            return new EndpointCandidateModel(
                null,
                ImmutableArray.Create(new ValiantDiagnosticInfo(UnsupportedEndpointShape, location, typeName)));
        }

        if (isGroup && isEndpoint)
        {
            return new EndpointCandidateModel(
                null,
                ImmutableArray.Create(new ValiantDiagnosticInfo(AmbiguousEndpointShape, location, typeName)));
        }

        if (isGroup)
        {
            return new EndpointCandidateModel(
                null,
                ImmutableArray.Create(new ValiantDiagnosticInfo(EndpointAttributeCannotMarkGroup, location, typeName)));
        }

        string? groupType = null;
        if (groupTypeSymbol is not null)
        {
            if (!ValiantSymbolMatching.ImplementsInterface(groupTypeSymbol, AspNetCoreNamespace, EndpointGroupMetadataName))
            {
                return new EndpointCandidateModel(
                    null,
                    ImmutableArray.Create(new ValiantDiagnosticInfo(
                        InvalidEndpointGroupType,
                        location,
                        typeName,
                        groupTypeSymbol.ToDisplayString(TypeDisplayFormat))));
            }

            groupType = groupTypeSymbol.ToDisplayString(TypeDisplayFormat);
        }

        var queryParsingPolicy = GetEndpointQueryParsingPolicy(typeSymbol, cancellationToken);
        var queryObjectBindings = CreateQueryObjectBindings(
            context.SemanticModel,
            typeSymbol,
            queryParsingPolicy.EnableDirectJsonQueryValues,
            queryParsingPolicy.JsonPolicyLocation,
            cancellationToken);
        return new EndpointCandidateModel(
            new EndpointModel(
                typeName,
                isGroup ? EndpointKind.Group : EndpointKind.Endpoint,
                groupType,
                order,
                queryParsingPolicy.Overrides),
            queryObjectBindings.Bindings,
            queryObjectBindings.Diagnostics);
    }

    private static EndpointCandidateModel CreateEndpointGroupCandidate(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return new EndpointCandidateModel(null, ImmutableArray<ValiantDiagnosticInfo>.Empty);
        }

        var isGroup = ValiantSymbolMatching.ImplementsInterface(typeSymbol, AspNetCoreNamespace, EndpointGroupMetadataName);
        var isEndpoint = ValiantSymbolMatching.ImplementsInterface(typeSymbol, AspNetCoreNamespace, EndpointMetadataName);
        var typeName = typeSymbol.ToDisplayString(TypeDisplayFormat);
        var order = GetEndpointOrder(context.Attributes);
        var location = typeSymbol.Locations.FirstOrDefault();

        if (isGroup && isEndpoint)
        {
            return new EndpointCandidateModel(
                null,
                ImmutableArray.Create(new ValiantDiagnosticInfo(AmbiguousEndpointShape, location, typeName)));
        }

        if (isEndpoint)
        {
            return new EndpointCandidateModel(
                null,
                ImmutableArray.Create(new ValiantDiagnosticInfo(EndpointGroupAttributeCannotMarkEndpoint, location, typeName)));
        }

        if (!isGroup)
        {
            return new EndpointCandidateModel(
                null,
                ImmutableArray.Create(new ValiantDiagnosticInfo(UnsupportedEndpointGroupShape, location, typeName)));
        }

        return new EndpointCandidateModel(
            new EndpointModel(
                typeName,
                EndpointKind.Group,
                null,
                order),
            ImmutableArray<ValiantDiagnosticInfo>.Empty);
    }

    private static (ImmutableArray<QueryObjectBindingModel> Bindings, ImmutableArray<ValiantDiagnosticInfo> Diagnostics) CreateQueryObjectBindings(
        SemanticModel semanticModel,
        INamedTypeSymbol endpointType,
        bool enableDirectJsonQueryValues,
        ValiantDiagnosticLocation jsonPolicyLocation,
        CancellationToken cancellationToken)
    {
        var bindings = ImmutableArray.CreateBuilder<QueryObjectBindingModel>();
        var diagnostics = ImmutableArray.CreateBuilder<ValiantDiagnosticInfo>();
        var bindingTypes = new HashSet<string>(StringComparer.Ordinal);
        var diagnosticKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var mapMethod in GetMapMethodDeclarations(endpointType, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var invocation in mapMethod
                .DescendantNodes(static node => node is not LambdaExpressionSyntax)
                .OfType<InvocationExpressionSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var handlerExpression in GetAspNetCoreRouteHandlerExpressions(semanticModel, invocation, cancellationToken))
                {
                    foreach (var parameter in GetHandlerParameters(handlerExpression))
                    {
                        if (parameter.AttributeLists.Count == 0)
                        {
                            if (semanticModel.GetDeclaredSymbol(parameter) is IParameterSymbol parameterWithoutAttributes &&
                                UnwrapNullable(parameterWithoutAttributes.Type) is INamedTypeSymbol unannotatedRequestType &&
                                HasFromQueryProperties(unannotatedRequestType))
                            {
                                AddQueryRequestBindingForType(
                                    unannotatedRequestType,
                                    parameterWithoutAttributes,
                                    bindings,
                                    diagnostics,
                                    bindingTypes,
                                    diagnosticKeys,
                                    cancellationToken);
                            }

                            continue;
                        }

                        if (semanticModel.GetDeclaredSymbol(parameter) is not IParameterSymbol parameterSymbol)
                        {
                            continue;
                        }

                        if (HasAttribute(parameterSymbol, FromQueryAttributeMetadataName))
                        {
                            AddQueryObjectBindingsForType(
                                QueryObjectBindingKind.TryParse,
                                parameterSymbol.Type,
                                parameterSymbol,
                                bindings,
                                diagnostics,
                                bindingTypes,
                                diagnosticKeys,
                                enableDirectJsonQueryValues,
                                jsonPolicyLocation,
                                cancellationToken);
                        }

                        if (HasAttribute(parameterSymbol, AsParametersAttributeMetadataName) &&
                            UnwrapNullable(parameterSymbol.Type) is INamedTypeSymbol requestType)
                        {
                            AddQueryObjectBindingsForRequestProperties(requestType, bindings, diagnostics, bindingTypes, diagnosticKeys, cancellationToken);
                        }
                    }
                }
            }
        }

        return (
            bindings
                .OrderBy(static binding => binding.Type, StringComparer.Ordinal)
                .ToImmutableArray(),
            diagnostics.ToImmutable());
    }

    private static IEnumerable<ExpressionSyntax> GetAspNetCoreRouteHandlerExpressions(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method ||
            !IsAspNetCoreRouteMappingMethod(method))
        {
            yield break;
        }

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (semanticModel.GetOperation(argument, cancellationToken) is IArgumentOperation { Parameter: { } parameter } &&
                IsRouteHandlerParameter(parameter))
            {
                yield return argument.Expression;
            }
        }
    }

    private static bool IsAspNetCoreRouteMappingMethod(IMethodSymbol method)
    {
        var originalMethod = method.ReducedFrom ?? method;
        var containingType = originalMethod.ContainingType;
        if (containingType is null ||
            containingType.ContainingNamespace.ToDisplayString() != "Microsoft.AspNetCore.Builder" ||
            containingType.Name is not ("EndpointRouteBuilderExtensions" or "RouteGroupBuilderExtensions"))
        {
            return false;
        }

        return originalMethod.Name.StartsWith("Map", StringComparison.Ordinal);
    }

    private static bool IsRouteHandlerParameter(IParameterSymbol parameter)
    {
        if (parameter.Type.SpecialType == SpecialType.System_Delegate)
        {
            return true;
        }

        return parameter.Type.ContainingNamespace.ToDisplayString() == "Microsoft.AspNetCore.Http" &&
            parameter.Type.MetadataName == "RequestDelegate";
    }

    private static IEnumerable<MethodDeclarationSyntax> GetMapMethodDeclarations(
        INamedTypeSymbol endpointType,
        CancellationToken cancellationToken)
    {
        foreach (var member in endpointType.GetMembers("Map"))
        {
            if (member is not IMethodSymbol
                {
                    IsStatic: true,
                    DeclaredAccessibility: Accessibility.Public,
                    Parameters.Length: 1
                } method)
            {
                continue;
            }

            foreach (var syntaxReference in method.DeclaringSyntaxReferences)
            {
                if (syntaxReference.GetSyntax(cancellationToken) is MethodDeclarationSyntax methodDeclaration)
                {
                    yield return methodDeclaration;
                }
            }
        }
    }

    private static IEnumerable<ParameterSyntax> GetHandlerParameters(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case ParenthesizedLambdaExpressionSyntax lambda:
                return lambda.ParameterList.Parameters;
            case SimpleLambdaExpressionSyntax lambda:
                return [lambda.Parameter];
            case AnonymousMethodExpressionSyntax { ParameterList: not null } anonymousMethod:
                return anonymousMethod.ParameterList.Parameters;
            default:
                return [];
        }
    }

    private static void AddQueryObjectBindingsForRequestProperties(
        INamedTypeSymbol requestType,
        ImmutableArray<QueryObjectBindingModel>.Builder bindings,
        ImmutableArray<ValiantDiagnosticInfo>.Builder diagnostics,
        HashSet<string> bindingTypes,
        HashSet<string> diagnosticKeys,
        CancellationToken cancellationToken)
    {
        foreach (var member in requestType.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (member is not IPropertySymbol
                {
                    IsStatic: false,
                    IsIndexer: false,
                    DeclaredAccessibility: Accessibility.Public
                } property ||
                !HasAttribute(property, FromQueryAttributeMetadataName))
            {
                continue;
            }

            AddQueryObjectBindingsForType(
                QueryObjectBindingKind.AsParametersTryParse,
                property.Type,
                property,
                bindings,
                diagnostics,
                bindingTypes,
                diagnosticKeys,
                true,
                ValiantDiagnosticLocation.Create(property.Locations.FirstOrDefault()),
                cancellationToken);
        }
    }

    private static bool HasFromQueryProperties(INamedTypeSymbol requestType)
    {
        foreach (var member in requestType.GetMembers())
        {
            if (member is IPropertySymbol
                {
                    IsStatic: false,
                    IsIndexer: false,
                    DeclaredAccessibility: Accessibility.Public
                } property &&
                HasAttribute(property, FromQueryAttributeMetadataName))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddQueryRequestBindingForType(
        INamedTypeSymbol requestType,
        ISymbol sourceSymbol,
        ImmutableArray<QueryObjectBindingModel>.Builder bindings,
        ImmutableArray<ValiantDiagnosticInfo>.Builder diagnostics,
        HashSet<string> bindingTypes,
        HashSet<string> diagnosticKeys,
        CancellationToken cancellationToken)
    {
        if (ShouldSkipQueryRequestBindingType(requestType))
        {
            return;
        }

        var bindingType = QueryObjectBindingKind.BindAsync + "|" + requestType.ToDisplayString(TypeDisplayFormat);
        if (bindingTypes.Contains(bindingType))
        {
            return;
        }

        if (!TryCreateQueryObjectBindingModel(
                QueryObjectBindingKind.BindAsync,
                requestType,
                sourceSymbol,
                true,
                ValiantDiagnosticLocation.Create(sourceSymbol.Locations.FirstOrDefault()),
                cancellationToken,
                out var binding,
                out var diagnostic))
        {
            if (diagnostic is { } diagnosticInfo)
            {
                var diagnosticKey = bindingType + "|" + diagnosticInfo.GetMessage();
                if (diagnosticKeys.Add(diagnosticKey))
                {
                    diagnostics.Add(diagnosticInfo);
                }
            }

            return;
        }

        if (bindingTypes.Add(bindingType))
        {
            bindings.Add(binding);
        }
    }

    private static void AddQueryObjectBindingsForType(
        QueryObjectBindingKind kind,
        ITypeSymbol queryType,
        ISymbol sourceSymbol,
        ImmutableArray<QueryObjectBindingModel>.Builder bindings,
        ImmutableArray<ValiantDiagnosticInfo>.Builder diagnostics,
        HashSet<string> bindingTypes,
        HashSet<string> diagnosticKeys,
        bool enableJsonQueryValues,
        ValiantDiagnosticLocation policyLocation,
        CancellationToken cancellationToken)
    {
        foreach (var queryObjectType in GetQueryObjectBindingTargets(queryType))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ShouldSkipQueryObjectType(queryObjectType))
            {
                continue;
            }

            var bindingType = kind + "|" + queryObjectType.ToDisplayString(TypeDisplayFormat);
            if (bindingTypes.Contains(bindingType))
            {
                continue;
            }

            if (!TryCreateQueryObjectBindingModel(
                    kind,
                    queryObjectType,
                    sourceSymbol,
                    enableJsonQueryValues,
                    policyLocation,
                    cancellationToken,
                    out var binding,
                    out var diagnostic))
            {
                if (diagnostic is { } diagnosticInfo)
                {
                    var diagnosticKey = bindingType + "|" + diagnosticInfo.GetMessage();
                    if (diagnosticKeys.Add(diagnosticKey))
                    {
                        diagnostics.Add(diagnosticInfo);
                    }
                }

                continue;
            }

            if (bindingTypes.Add(bindingType))
            {
                bindings.Add(binding);
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetQueryObjectBindingTargets(ITypeSymbol queryType)
    {
        queryType = UnwrapNullable(queryType);
        if (queryType is IArrayTypeSymbol arrayType)
        {
            foreach (var target in GetQueryObjectBindingTargets(arrayType.ElementType))
            {
                yield return target;
            }

            yield break;
        }

        if (queryType is not INamedTypeSymbol namedType)
        {
            yield break;
        }

        if (TryGetDictionaryArguments(namedType, out var keyType, out var valueType))
        {
            foreach (var target in GetQueryObjectBindingTargets(keyType))
            {
                yield return target;
            }

            foreach (var target in GetQueryObjectBindingTargets(valueType))
            {
                yield return target;
            }

            yield break;
        }

        if (TryGetCollectionElementType(namedType, out var elementType))
        {
            foreach (var target in GetQueryObjectBindingTargets(elementType))
            {
                yield return target;
            }

            yield break;
        }

        yield return namedType;
    }

    private static bool TryGetCollectionElementType(INamedTypeSymbol type, out ITypeSymbol elementType)
    {
        if (IsGenericType(type, "System.Collections.Generic.IEnumerable`1") ||
            IsGenericType(type, "System.Collections.Generic.IReadOnlyCollection`1") ||
            IsGenericType(type, "System.Collections.Generic.ICollection`1") ||
            IsGenericType(type, "System.Collections.Generic.IReadOnlyList`1") ||
            IsGenericType(type, "System.Collections.Generic.IList`1") ||
            IsGenericType(type, "System.Collections.Generic.List`1") ||
            IsGenericType(type, "System.Collections.Generic.HashSet`1") ||
            IsGenericType(type, "System.Collections.Generic.ISet`1"))
        {
            elementType = type.TypeArguments[0];
            return true;
        }

        foreach (var @interface in type.AllInterfaces)
        {
            if (IsGenericType(@interface, "System.Collections.Generic.IEnumerable`1"))
            {
                elementType = @interface.TypeArguments[0];
                return true;
            }
        }

        elementType = type;
        return false;
    }

    private static bool TryGetDictionaryArguments(
        INamedTypeSymbol type,
        out ITypeSymbol keyType,
        out ITypeSymbol valueType)
    {
        if (IsGenericType(type, "System.Collections.Generic.Dictionary`2") ||
            IsGenericType(type, "System.Collections.Generic.IDictionary`2") ||
            IsGenericType(type, "System.Collections.Generic.IReadOnlyDictionary`2"))
        {
            keyType = type.TypeArguments[0];
            valueType = type.TypeArguments[1];
            return true;
        }

        foreach (var @interface in type.AllInterfaces)
        {
            if (IsGenericType(@interface, "System.Collections.Generic.IDictionary`2") ||
                IsGenericType(@interface, "System.Collections.Generic.IReadOnlyDictionary`2"))
            {
                keyType = @interface.TypeArguments[0];
                valueType = @interface.TypeArguments[1];
                return true;
            }
        }

        keyType = type;
        valueType = type;
        return false;
    }

    private static bool IsGenericType(INamedTypeSymbol type, string metadataName)
    {
        var originalDefinition = type.OriginalDefinition;
        var fullMetadataName = originalDefinition.ContainingNamespace.IsGlobalNamespace
            ? originalDefinition.MetadataName
            : originalDefinition.ContainingNamespace.ToDisplayString() + "." + originalDefinition.MetadataName;
        return string.Equals(fullMetadataName, metadataName, StringComparison.Ordinal);
    }

    private static bool ShouldSkipQueryObjectType(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum ||
            type.SpecialType != SpecialType.None ||
            type.TypeKind == TypeKind.Delegate ||
            type.TypeKind == TypeKind.Interface ||
            HasTryParse(type) ||
            ImplementsIParsable(type))
        {
            return true;
        }

        return false;
    }

    private static bool ShouldSkipQueryRequestBindingType(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum ||
            type.SpecialType != SpecialType.None ||
            type.TypeKind == TypeKind.Delegate ||
            type.TypeKind == TypeKind.Interface ||
            HasBindAsync(type))
        {
            return true;
        }

        return false;
    }

    private static bool TryCreateQueryObjectBindingModel(
        QueryObjectBindingKind kind,
        INamedTypeSymbol type,
        ISymbol sourceSymbol,
        bool enableJsonQueryValues,
        ValiantDiagnosticLocation policyLocation,
        CancellationToken cancellationToken,
        out QueryObjectBindingModel binding,
        out ValiantDiagnosticInfo? diagnostic)
    {
        binding = default;
        diagnostic = null;

        var typeName = type.ToDisplayString(TypeDisplayFormat);
        if (type.Locations.All(static location => !location.IsInSource))
        {
            diagnostic = CreateUnsupportedQueryObjectBindingDiagnostic(type, sourceSymbol, "it is not declared in source");
            return false;
        }

        if (type.IsStatic)
        {
            diagnostic = CreateUnsupportedQueryObjectBindingDiagnostic(type, sourceSymbol, "static types cannot define query object parsers");
            return false;
        }

        if (type.TypeParameters.Length > 0)
        {
            diagnostic = CreateUnsupportedQueryObjectBindingDiagnostic(type, sourceSymbol, "generic query object types are not supported yet");
            return false;
        }

        for (var containingType = type.ContainingType; containingType is not null; containingType = containingType.ContainingType)
        {
            if (containingType.TypeParameters.Length > 0)
            {
                diagnostic = CreateUnsupportedQueryObjectBindingDiagnostic(type, sourceSymbol, "query object types nested inside generic containing types are not supported yet");
                return false;
            }
        }

        if (!HasPartialDeclarations(type, cancellationToken))
        {
            diagnostic = CreateUnsupportedQueryObjectBindingDiagnostic(type, sourceSymbol, "it must be declared partial");
            return false;
        }

        for (var containingType = type.ContainingType; containingType is not null; containingType = containingType.ContainingType)
        {
            if (!HasPartialDeclarations(containingType, cancellationToken))
            {
                diagnostic = CreateUnsupportedQueryObjectBindingDiagnostic(type, sourceSymbol, "containing types must be declared partial");
                return false;
            }
        }

        var containingTypes = ImmutableArray.CreateBuilder<QueryObjectContainingTypeModel>();
        var containingTypeStack = new Stack<INamedTypeSymbol>();
        for (var containingType = type.ContainingType; containingType is not null; containingType = containingType.ContainingType)
        {
            containingTypeStack.Push(containingType);
        }

        while (containingTypeStack.Count > 0)
        {
            var containingType = containingTypeStack.Pop();
            containingTypes.Add(new QueryObjectContainingTypeModel(
                ValiantTypeDeclarations.GetTypeModifier(containingType),
                ValiantTypeDeclarations.GetTypeDeclarationKeyword(containingType),
                ValiantTypeDeclarations.GetTypeDeclarationName(containingType)));
        }

        binding = new QueryObjectBindingModel(
            kind,
            GetHintName(type, kind == QueryObjectBindingKind.BindAsync ? "QueryRequest" : "QueryObject"),
            type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            ValiantTypeDeclarations.GetTypeModifier(type),
            ValiantTypeDeclarations.GetTypeDeclarationKeyword(type),
            ValiantTypeDeclarations.GetTypeDeclarationName(type),
            typeName,
            type.IsValueType,
            enableJsonQueryValues,
            policyLocation,
            containingTypes.ToImmutable());
        return true;
    }

    private static ValiantDiagnosticInfo CreateUnsupportedQueryObjectBindingDiagnostic(
        INamedTypeSymbol type,
        ISymbol sourceSymbol,
        string reason)
    {
        return new ValiantDiagnosticInfo(
            UnsupportedQueryObjectBindingShape,
            sourceSymbol.Locations.FirstOrDefault() ?? type.Locations.FirstOrDefault(),
            type.ToDisplayString(TypeDisplayFormat),
            reason);
    }

    private static bool HasPartialDeclarations(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var hasSyntax = false;
        foreach (var syntaxReference in type.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax(cancellationToken) is not TypeDeclarationSyntax declaration)
            {
                continue;
            }

            hasSyntax = true;
            if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return false;
            }
        }

        return hasSyntax;
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        var unwrapped = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType
            ? nullableType.TypeArguments[0]
            : type;
        return unwrapped.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
    }

    private static bool HasTryParse(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers("TryParse"))
        {
            if (member is not IMethodSymbol
                {
                    IsStatic: true,
                    DeclaredAccessibility: Accessibility.Public,
                    ReturnType.SpecialType: SpecialType.System_Boolean
                } method)
            {
                continue;
            }

            if (method.Parameters.Length == 2 &&
                method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                method.Parameters[1].RefKind == RefKind.Out &&
                SymbolEqualityComparer.Default.Equals(UnwrapNullable(method.Parameters[1].Type), type))
            {
                return true;
            }

            if (method.Parameters.Length == 3 &&
                method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                method.Parameters[1].Type.Name == "IFormatProvider" &&
                method.Parameters[1].Type.ContainingNamespace.Name == "System" &&
                method.Parameters[2].RefKind == RefKind.Out &&
                SymbolEqualityComparer.Default.Equals(UnwrapNullable(method.Parameters[2].Type), type))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBindAsync(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers("BindAsync"))
        {
            if (member is IMethodSymbol
                {
                    IsStatic: true,
                    DeclaredAccessibility: Accessibility.Public
                })
            {
                return true;
            }
        }

        return false;
    }

    private static bool ImplementsIParsable(INamedTypeSymbol type)
    {
        foreach (var @interface in type.AllInterfaces)
        {
            var originalDefinition = @interface.OriginalDefinition;
            var fullMetadataName = originalDefinition.ContainingNamespace.IsGlobalNamespace
                ? originalDefinition.MetadataName
                : originalDefinition.ContainingNamespace.ToDisplayString() + "." + originalDefinition.MetadataName;
            if (string.Equals(fullMetadataName, "System.IParsable`1", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static INamedTypeSymbol? GetEndpointGroupTypeSymbol(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass is { TypeArguments.Length: 1 } attributeClass &&
                IsAttribute(attributeClass, GenericEndpointAttributeMetadataName) &&
                attributeClass.TypeArguments[0] is INamedTypeSymbol genericGroupType)
            {
                return genericGroupType;
            }

            if (attribute.ConstructorArguments.Length != 1 ||
                attribute.ConstructorArguments[0].Value is not INamedTypeSymbol groupType)
            {
                continue;
            }

            return groupType;
        }

        return null;
    }

    private static bool HasEndpointAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is not null &&
                (IsAttribute(attribute.AttributeClass, EndpointAttributeMetadataName) ||
                    IsAttribute(attribute.AttributeClass, GenericEndpointAttributeMetadataName)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAttribute(INamedTypeSymbol typeSymbol, string metadataName)
    {
        return HasAttribute((ISymbol)typeSymbol, metadataName);
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not null && IsAttribute(attribute.AttributeClass, metadataName))
            {
                return true;
            }
        }

        return false;
    }

    private static (
        EndpointQueryParsingOverridesModel Overrides,
        bool EnableDirectJsonQueryValues,
        ValiantDiagnosticLocation JsonPolicyLocation) GetEndpointQueryParsingPolicy(
        INamedTypeSymbol endpointType,
        CancellationToken cancellationToken)
    {
        var fallbackLocation = ValiantDiagnosticLocation.Create(endpointType.Locations.FirstOrDefault());
        foreach (var attribute in endpointType.GetAttributes())
        {
            if (attribute.AttributeClass is null ||
                !IsAttribute(attribute.AttributeClass, QueryParsingAttributeMetadataName))
            {
                continue;
            }

            var supportedArrayFormats = default(ImmutableArray<int>);
            var supportedObjectFormats = default(ImmutableArray<int>);
            bool? enableJsonQueryValuesOverride = null;
            var enableDirectJsonQueryValues = true;
            foreach (var namedArgument in attribute.NamedArguments)
            {
                switch (namedArgument.Key)
                {
                    case "SupportedArrayFormats":
                        supportedArrayFormats = GetEnumArrayValues(namedArgument.Value);
                        break;
                    case "SupportedObjectFormats":
                        supportedObjectFormats = GetEnumArrayValues(namedArgument.Value);
                        break;
                    case "JsonValues" when namedArgument.Value.Value is int jsonValues:
                        enableDirectJsonQueryValues = jsonValues != 0;
                        enableJsonQueryValuesOverride = enableDirectJsonQueryValues;
                        break;
                }
            }

            var location = fallbackLocation;
            if (attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is AttributeSyntax attributeSyntax)
            {
                var valueExpression = attributeSyntax.ArgumentList?.Arguments
                    .FirstOrDefault(static argument =>
                        argument.NameEquals?.Name.Identifier.ValueText == "JsonValues")
                    ?.Expression;
                location = ValiantDiagnosticLocation.Create(
                    valueExpression?.GetLocation() ?? attributeSyntax.GetLocation());
            }

            return (
                new EndpointQueryParsingOverridesModel(
                    supportedArrayFormats,
                    supportedObjectFormats,
                    enableJsonQueryValuesOverride),
                enableDirectJsonQueryValues,
                location);
        }

        return (default, true, fallbackLocation);
    }

    private static ImmutableArray<int> GetEnumArrayValues(TypedConstant constant)
    {
        if (constant.IsNull || constant.Kind != TypedConstantKind.Array)
        {
            return default;
        }

        var builder = ImmutableArray.CreateBuilder<int>(constant.Values.Length);
        foreach (var value in constant.Values)
        {
            if (value.Value is int enumValue)
            {
                builder.Add(enumValue);
            }
        }

        return builder.ToImmutable();
    }

    private static int CountEndpointAttributes(INamedTypeSymbol typeSymbol)
    {
        var count = 0;
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is not null &&
                (IsAttribute(attribute.AttributeClass, EndpointAttributeMetadataName) ||
                    IsAttribute(attribute.AttributeClass, GenericEndpointAttributeMetadataName)))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsAttribute(INamedTypeSymbol attributeClass, string metadataName)
    {
        var originalDefinition = attributeClass.OriginalDefinition;
        var fullMetadataName = originalDefinition.ContainingNamespace.IsGlobalNamespace
            ? originalDefinition.MetadataName
            : originalDefinition.ContainingNamespace.ToDisplayString() + "." + originalDefinition.MetadataName;
        return string.Equals(fullMetadataName, metadataName, StringComparison.Ordinal);
    }

    private static string GetHintName(INamedTypeSymbol symbol, string suffix)
    {
        return ValiantHintNames.Create(symbol, suffix);
    }

    private static int GetEndpointOrder(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            foreach (var namedArgument in attribute.NamedArguments)
            {
                if (namedArgument.Key == "Order" &&
                    namedArgument.Value.Value is int order)
                {
                    return order;
                }
            }
        }

        return 0;
    }

    private static void ExecuteDiagnostics(
        SourceProductionContext context,
        EndpointCandidateModel candidate,
        bool includeQueryBindingDiagnostics = false)
    {
        foreach (var diagnostic in candidate.Diagnostics)
        {
            if (!includeQueryBindingDiagnostics && diagnostic.Descriptor.Id == UnsupportedQueryObjectBindingShape.Id)
            {
                continue;
            }

            context.ReportDiagnostic(diagnostic.Create());
        }
    }

    private static bool HasDiagnostics(
        ImmutableArray<EndpointCandidateModel> candidates,
        bool includeQueryBindingDiagnostics)
    {
        foreach (var candidate in candidates)
        {
            foreach (var diagnostic in candidate.Diagnostics)
            {
                if (includeQueryBindingDiagnostics || diagnostic.Descriptor.Id != UnsupportedQueryObjectBindingShape.Id)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void Execute(
        SourceProductionContext context,
        EndpointRegistrationModel registration)
    {
        if (registration.IsEmpty)
        {
            return;
        }

        context.AddSource(
            GetEndpointRegistrationHintName(
                registration.Endpoints,
                registration.QueryBindingGenerated),
            SourceText.From(
                Generate(
                    registration.Endpoints,
                    registration.QueryBindingGenerated),
                Encoding.UTF8));
    }

    private static void ExecuteQueryObjectBindings(
        SourceProductionContext context,
        QueryObjectBindingRegistrationModel registration)
    {
        if (registration.IsEmpty)
        {
            return;
        }

        foreach (var binding in registration.Bindings)
        {
            context.AddSource(binding.HintName, SourceText.From(GenerateQueryBinding(binding), Encoding.UTF8));
        }

        foreach (var conflict in registration.Conflicts)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ConflictingQueryJsonPolicies,
                conflict.Location.ToLocation(),
                conflict.Type));
        }
    }

    private static EndpointRegistrationModel CreateEndpointRegistrationModel(
        ImmutableArray<EndpointModel> endpoints,
        ImmutableArray<EndpointModel> genericEndpoints,
        ImmutableArray<EndpointModel> endpointGroups,
        bool includeQueryParsingOverrides)
    {
        if (endpoints.IsDefaultOrEmpty && genericEndpoints.IsDefaultOrEmpty && endpointGroups.IsDefaultOrEmpty)
        {
            return new EndpointRegistrationModel(
                ImmutableArray<EndpointModel>.Empty,
                includeQueryParsingOverrides);
        }

        var builder = ImmutableArray.CreateBuilder<EndpointModel>(endpoints.Length + genericEndpoints.Length + endpointGroups.Length);
        builder.AddRange(endpoints);
        builder.AddRange(genericEndpoints);
        builder.AddRange(endpointGroups);

        var distinctEndpoints = builder
            .Select(endpoint => includeQueryParsingOverrides || endpoint.QueryParsingOverrides.IsEmpty
                ? endpoint
                : new EndpointModel(
                    endpoint.Type,
                    endpoint.Kind,
                    endpoint.GroupType,
                    endpoint.Order))
            .Distinct()
            .OrderBy(static endpoint => endpoint.Kind)
            .ThenBy(static endpoint => endpoint.Order)
            .ThenBy(static endpoint => endpoint.GroupType, StringComparer.Ordinal)
            .ThenBy(static endpoint => endpoint.Type, StringComparer.Ordinal)
            .ToImmutableArray();

        return new EndpointRegistrationModel(
            distinctEndpoints,
            includeQueryParsingOverrides);
    }

    private static string GetEndpointRegistrationHintName(
        ImmutableArray<EndpointModel> endpoints,
        bool queryBindingGenerated)
    {
        unchecked
        {
            var hash = AppendHash(17, queryBindingGenerated ? 1 : 0);
            foreach (var endpoint in endpoints)
            {
                hash = AppendHash(hash, (int)endpoint.Kind);
                hash = AppendHash(hash, endpoint.Order);
                hash = AppendHash(hash, endpoint.GroupType);
                hash = AppendHash(hash, endpoint.Type);
                if (!endpoint.QueryParsingOverrides.IsEmpty)
                {
                    hash = AppendHash(hash, 1);
                    hash = AppendHash(hash, endpoint.QueryParsingOverrides.SupportedArrayFormats);
                    hash = AppendHash(hash, endpoint.QueryParsingOverrides.SupportedObjectFormats);
                    hash = AppendHash(hash, endpoint.QueryParsingOverrides.EnableJsonQueryValues switch
                    {
                        true => 1,
                        false => 0,
                        null => -1
                    });
                }
            }

            return "StaticNorth.Valiant.Endpoints.AspNetCore." +
                ((uint)hash).ToString("X8", System.Globalization.CultureInfo.InvariantCulture) +
                ".g.cs";
        }
    }

    private static int AppendHash(int hash, int value)
    {
        unchecked
        {
            return (hash * 397) ^ value;
        }
    }

    private static int AppendHash(int hash, string? value)
    {
        unchecked
        {
            if (value is null)
            {
                return hash * 397;
            }

            foreach (var character in value)
            {
                hash = (hash * 397) ^ character;
            }

            return hash;
        }
    }

    private static int AppendHash(int hash, ImmutableArray<int> values)
    {
        unchecked
        {
            hash = (hash * 397) ^ values.IsDefault.GetHashCode();
            if (!values.IsDefault)
            {
                foreach (var value in values)
                {
                    hash = AppendHash(hash, value);
                }
            }

            return hash;
        }
    }

    private static QueryObjectBindingRegistrationModel CreateQueryObjectBindingRegistrationModel(
        ImmutableArray<QueryObjectBindingRegistrationModel> endpoints,
        ImmutableArray<QueryObjectBindingRegistrationModel> genericEndpoints)
    {
        if (endpoints.IsDefaultOrEmpty && genericEndpoints.IsDefaultOrEmpty)
        {
            return new QueryObjectBindingRegistrationModel(ImmutableArray<QueryObjectBindingModel>.Empty);
        }

        var builder = ImmutableArray.CreateBuilder<QueryObjectBindingModel>();
        foreach (var endpoint in endpoints)
        {
            builder.AddRange(endpoint.Bindings);
        }

        foreach (var endpoint in genericEndpoints)
        {
            builder.AddRange(endpoint.Bindings);
        }

        var conflictingTypes = builder
            .Where(static binding => binding.Kind == QueryObjectBindingKind.TryParse)
            .GroupBy(static binding => binding.Type, StringComparer.Ordinal)
            .Where(static group => group.Select(static binding => binding.EnableJsonQueryValues).Distinct().Count() > 1)
            .Select(static group => new QueryObjectBindingConflictModel(
                group.Key,
                group.OrderBy(static binding => binding.PolicyLocation.ToString(), StringComparer.Ordinal)
                    .First()
                    .PolicyLocation))
            .OrderBy(static conflict => conflict.Type, StringComparer.Ordinal)
            .ToImmutableArray();
        var conflictingTypeNames = new HashSet<string>(
            conflictingTypes.Select(static conflict => conflict.Type),
            StringComparer.Ordinal);
        var distinctBindings = builder
            .Where(binding => binding.Kind != QueryObjectBindingKind.TryParse ||
                !conflictingTypeNames.Contains(binding.Type))
            .GroupBy(static binding => (binding.Kind, binding.Type, binding.EnableJsonQueryValues))
            .Select(static group => group
                .OrderBy(static binding => binding.PolicyLocation.ToString(), StringComparer.Ordinal)
                .First())
            .ToArray();
        var realTryParseTypes = new HashSet<string>(
            distinctBindings
                .Where(static binding => binding.Kind == QueryObjectBindingKind.TryParse)
                .Select(static binding => binding.Type),
            StringComparer.Ordinal);

        return new QueryObjectBindingRegistrationModel(
            distinctBindings
                .Where(binding => binding.Kind != QueryObjectBindingKind.AsParametersTryParse ||
                    !realTryParseTypes.Contains(binding.Type))
                .OrderBy(static binding => binding.Type, StringComparer.Ordinal)
                .ThenBy(static binding => binding.Kind)
                .ToImmutableArray(),
            conflictingTypes);
    }

    private static string Generate(
        ImmutableArray<EndpointModel> endpoints,
        bool queryBindingGenerated)
    {
        var builder = new StringBuilder();
        ValiantSourceBuilder.AppendGeneratedFileHeader(builder);
        builder.AppendLine("[assembly: global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointRouteRegistrarAttribute(typeof(global::StaticNorth.Valiant.Endpoints.AspNetCore.Generated.ValiantEndpointRouteRegistrar))]");
        builder.AppendLine();
        builder.AppendLine("namespace StaticNorth.Valiant.Endpoints.AspNetCore.Generated");
        builder.AppendLine("{");
        builder.AppendLine("    internal sealed class ValiantEndpointRouteRegistrar : global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.IValiantEndpointRouteRegistrar");
        builder.AppendLine("    {");
        builder.AppendLine("        public ValiantEndpointRouteRegistrar()");
        builder.AppendLine("        {");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public void MapEndpoints(global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder builder)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (builder is null)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::System.ArgumentNullException(nameof(builder));");
        builder.AppendLine("            }");
        builder.AppendLine();

        var groups = CreateGroupRegistrations(endpoints);
        var groupIndexes = new Dictionary<string, int>(groups.Length, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            groupIndexes.Add(group.Type, group.Index);
        }

        for (var index = 0; index < groups.Length; index++)
        {
            builder.Append("            global::StaticNorth.Valiant.Endpoints.AspNetCore.EndpointGroupRegistration groupRegistration")
                .Append(groups[index].Index.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(" = global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.CreateEndpointGroupRegistration<")
                .Append(groups[index].Type)
                .Append(">(")
                .Append(groups[index].Order.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .AppendLine(");");
            builder.Append("            global::Microsoft.AspNetCore.Routing.RouteGroupBuilder? group")
                .Append(groups[index].Index.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .AppendLine(" = null;");
            builder.Append("            if (global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.ShouldMapEndpointGroup<")
                .Append(groups[index].Type)
                .Append(">(builder, groupRegistration")
                .Append(groups[index].Index.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .AppendLine("))");
            builder.AppendLine("            {");
            builder.Append("                group")
                .Append(groups[index].Index.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(" = global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.MapEndpointGroup<")
                .Append(groups[index].Type)
                .Append(">(builder, groupRegistration")
                .Append(groups[index].Index.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .AppendLine(");");
            builder.AppendLine("            }");
        }

        if (groups.Length > 0)
        {
            builder.AppendLine();
        }

        var hasRootEndpoints = false;
        for (var endpointIndex = 0; endpointIndex < endpoints.Length; endpointIndex++)
        {
            var endpoint = endpoints[endpointIndex];
            if (endpoint.Kind != EndpointKind.Endpoint)
            {
                continue;
            }

            if (endpoint.GroupType is null)
            {
                hasRootEndpoints = true;
                continue;
            }

            var groupIndex = groupIndexes[endpoint.GroupType];
            builder.Append("            if (group")
                .Append(groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .AppendLine(" is not null)");
            builder.AppendLine("            {");
            AppendCreateEndpointRegistration(
                builder,
                "                ",
                endpoint,
                endpointIndex,
                "group" + groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "groupRegistration" + groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                queryBindingGenerated);
            builder.Append("                if (global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.ShouldMapEndpoint<")
                .Append(endpoint.Type)
                .Append(">(group")
                .Append(groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(", endpointRegistration")
                .Append(endpointIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .AppendLine("))");
            builder.AppendLine("                {");
            AppendMapEndpoint(
                builder,
                "                    ",
                endpoint,
                "group" + groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "endpointRegistration" + endpointIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.AppendLine("                }");
            builder.AppendLine("            }");
        }

        if (groups.Length > 0 && hasRootEndpoints)
        {
            builder.AppendLine();
        }

        for (var endpointIndex = 0; endpointIndex < endpoints.Length; endpointIndex++)
        {
            var endpoint = endpoints[endpointIndex];
            if (endpoint is not { Kind: EndpointKind.Endpoint, GroupType: null })
            {
                continue;
            }

            AppendCreateEndpointRegistration(
                builder,
                "            ",
                endpoint,
                endpointIndex,
                "builder",
                "null",
                queryBindingGenerated);
            builder.Append("            if (global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.ShouldMapEndpoint<")
                .Append(endpoint.Type)
                .Append(">(builder, endpointRegistration")
                .Append(endpointIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .AppendLine("))");
            builder.AppendLine("            {");
            AppendMapEndpoint(
                builder,
                "                ",
                endpoint,
                "builder",
                "endpointRegistration" + endpointIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.AppendLine("            }");
        }

        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void AppendMapEndpoint(
        StringBuilder builder,
        string indent,
        EndpointModel endpoint,
        string routeBuilderExpression,
        string registrationExpression)
    {
        builder.Append(indent);
        if (!endpoint.QueryParsingOverrides.IsEmpty)
        {
            builder.Append("global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.ApplyQueryParsingOverrides(");
        }

        builder.Append("global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.MapEndpoint<")
            .Append(endpoint.Type)
            .Append(">(")
            .Append(routeBuilderExpression)
            .Append(", ")
            .Append(registrationExpression)
            .Append(')');

        if (!endpoint.QueryParsingOverrides.IsEmpty)
        {
            builder.Append(", ");
            AppendEnumArray(
                builder,
                endpoint.QueryParsingOverrides.SupportedArrayFormats,
                "global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryArrayFormat");
            builder.Append(", ");
            AppendEnumArray(
                builder,
                endpoint.QueryParsingOverrides.SupportedObjectFormats,
                "global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryObjectFormat");
            builder.Append(", ")
                .Append(endpoint.QueryParsingOverrides.EnableJsonQueryValues switch
                {
                    true => "true",
                    false => "false",
                    null => "null"
                })
                .Append(')');
        }

        builder.AppendLine(";");
    }

    private static void AppendCreateEndpointRegistration(
        StringBuilder builder,
        string indent,
        EndpointModel endpoint,
        int endpointIndex,
        string routeBuilderExpression,
        string groupRegistrationExpression,
        bool queryBindingGenerated)
    {
        builder.Append(indent)
            .Append("global::StaticNorth.Valiant.Endpoints.AspNetCore.EndpointRegistration endpointRegistration")
            .Append(endpointIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(" = global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.CreateEndpointRegistration<")
            .Append(endpoint.Type)
            .Append(">(")
            .Append(routeBuilderExpression)
            .Append(", ")
            .Append(groupRegistrationExpression)
            .Append(", ")
            .Append(endpoint.Order.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(", ")
            .Append(queryBindingGenerated ? "true" : "false")
            .Append(", ");
        AppendEnumArray(
            builder,
            endpoint.QueryParsingOverrides.SupportedArrayFormats,
            "global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryArrayFormat");
        builder.Append(", ");
        AppendEnumArray(
            builder,
            endpoint.QueryParsingOverrides.SupportedObjectFormats,
            "global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryObjectFormat");
        builder.Append(", ")
            .Append(endpoint.QueryParsingOverrides.EnableJsonQueryValues switch
            {
                true => "true",
                false => "false",
                null => "null"
            })
            .AppendLine(");");
    }

    private static void AppendEnumArray(
        StringBuilder builder,
        ImmutableArray<int> values,
        string enumType)
    {
        if (values.IsDefault)
        {
            builder.Append("null");
            return;
        }

        builder.Append("new ")
            .Append(enumType)
            .Append("[] { ");
        for (var index = 0; index < values.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append('(')
                .Append(enumType)
                .Append(')')
                .Append(values[index].ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        builder.Append(" }");
    }

    private static string GenerateQueryBinding(QueryObjectBindingModel binding)
    {
        return binding.Kind switch
        {
            QueryObjectBindingKind.BindAsync => GenerateQueryRequestBinding(binding),
            QueryObjectBindingKind.AsParametersTryParse => GenerateAsParametersQueryObjectBinding(binding),
            _ => GenerateQueryObjectBinding(binding)
        };
    }

    private static string GenerateAsParametersQueryObjectBinding(QueryObjectBindingModel binding)
    {
        var builder = new StringBuilder();
        ValiantSourceBuilder.AppendGeneratedFileHeader(builder);
        builder.AppendLine();

        if (binding.NamespaceName is not null)
        {
            builder.Append("namespace ").Append(binding.NamespaceName).AppendLine();
            builder.AppendLine("{");
        }

        var containingTypeCount = ValiantSourceBuilder.AppendContainingTypeDeclarations(
            builder,
            binding.NamespaceName,
            binding.ContainingTypes,
            static containingType => containingType.Modifier,
            static containingType => containingType.Keyword,
            static containingType => containingType.Name);

        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount, binding.NamespaceName);
        builder.Append(binding.Modifier)
            .Append("partial ")
            .Append(binding.Keyword)
            .Append(' ')
            .Append(binding.Name)
            .AppendLine();
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount, binding.NamespaceName);
        builder.AppendLine("{");

        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 1, binding.NamespaceName);
        builder.Append("public static bool TryParse(string value, out ")
            .Append(binding.Type);
        if (!binding.IsValueType)
        {
            builder.Append('?');
        }

        builder.AppendLine(" result)");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 1, binding.NamespaceName);
        builder.AppendLine("{");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("// Compatibility shim for ASP.NET Core [AsParameters] property binding.");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("// Valiant reparses the full query string in an endpoint filter before the handler runs.");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("result = default;");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("return true;");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 1, binding.NamespaceName);
        builder.AppendLine("}");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount, binding.NamespaceName);
        builder.AppendLine("}");

        ValiantSourceBuilder.AppendContainingTypeClosures(builder, binding.NamespaceName, containingTypeCount);

        if (binding.NamespaceName is not null)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static string GenerateQueryObjectBinding(QueryObjectBindingModel binding)
    {
        var builder = new StringBuilder();
        ValiantSourceBuilder.AppendGeneratedFileHeader(builder);
        builder.AppendLine();

        if (binding.NamespaceName is not null)
        {
            builder.Append("namespace ").Append(binding.NamespaceName).AppendLine();
            builder.AppendLine("{");
        }

        var containingTypeCount = ValiantSourceBuilder.AppendContainingTypeDeclarations(
            builder,
            binding.NamespaceName,
            binding.ContainingTypes,
            static containingType => containingType.Modifier,
            static containingType => containingType.Keyword,
            static containingType => containingType.Name);

        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount, binding.NamespaceName);
        builder.Append(binding.Modifier)
            .Append("partial ")
            .Append(binding.Keyword)
            .Append(' ')
            .Append(binding.Name)
            .AppendLine();
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount, binding.NamespaceName);
        builder.AppendLine("{");

        if (binding.EnableJsonQueryValues)
        {
            ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 1, binding.NamespaceName);
            builder.Append("private static readonly global::System.Text.Json.JsonSerializerOptions __valiantQueryObjectJsonSerializerOptions = new global::System.Text.Json.JsonSerializerOptions(global::System.Text.Json.JsonSerializerDefaults.Web);")
                .AppendLine();
            builder.AppendLine();
        }

        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 1, binding.NamespaceName);
        builder.Append("public static bool TryParse(string value, out ")
            .Append(binding.Type)
            .AppendLine(" result)");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 1, binding.NamespaceName);
        builder.AppendLine("{");

        if (!binding.EnableJsonQueryValues)
        {
            ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
            builder.AppendLine("result = default!;");
            ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
            builder.AppendLine("throw new global::Microsoft.AspNetCore.Http.BadHttpRequestException(\"JSON query values are disabled for this endpoint.\", global::Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest);");
            ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 1, binding.NamespaceName);
            builder.AppendLine("}");
            ValiantSourceBuilder.AppendIndent(builder, containingTypeCount, binding.NamespaceName);
            builder.AppendLine("}");

            ValiantSourceBuilder.AppendContainingTypeClosures(builder, binding.NamespaceName, containingTypeCount);

            if (binding.NamespaceName is not null)
            {
                builder.AppendLine("}");
            }

            return builder.ToString();
        }

        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("try");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("{");

        if (binding.IsValueType)
        {
            ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 3, binding.NamespaceName);
            builder.Append("result = global::System.Text.Json.JsonSerializer.Deserialize<")
                .Append(binding.Type)
                .AppendLine(">(value, __valiantQueryObjectJsonSerializerOptions)!;");
            ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 3, binding.NamespaceName);
            builder.AppendLine("return true;");
        }
        else
        {
            ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 3, binding.NamespaceName);
            builder.Append("result = global::System.Text.Json.JsonSerializer.Deserialize<")
                .Append(binding.Type)
                .AppendLine(">(value, __valiantQueryObjectJsonSerializerOptions)!;");
            ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 3, binding.NamespaceName);
            builder.AppendLine("return result is not null;");
        }

        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("}");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("catch (global::System.Exception)");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("{");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 3, binding.NamespaceName);
        builder.AppendLine("result = default!;");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 3, binding.NamespaceName);
        builder.AppendLine("throw new global::Microsoft.AspNetCore.Http.BadHttpRequestException(\"The query value is not valid JSON for this parameter.\", global::Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest);");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("}");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 1, binding.NamespaceName);
        builder.AppendLine("}");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount, binding.NamespaceName);
        builder.AppendLine("}");

        ValiantSourceBuilder.AppendContainingTypeClosures(builder, binding.NamespaceName, containingTypeCount);

        if (binding.NamespaceName is not null)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static string GenerateQueryRequestBinding(QueryObjectBindingModel binding)
    {
        var builder = new StringBuilder();
        ValiantSourceBuilder.AppendGeneratedFileHeader(builder);
        builder.AppendLine();

        if (binding.NamespaceName is not null)
        {
            builder.Append("namespace ").Append(binding.NamespaceName).AppendLine();
            builder.AppendLine("{");
        }

        var containingTypeCount = ValiantSourceBuilder.AppendContainingTypeDeclarations(
            builder,
            binding.NamespaceName,
            binding.ContainingTypes,
            static containingType => containingType.Modifier,
            static containingType => containingType.Keyword,
            static containingType => containingType.Name);

        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount, binding.NamespaceName);
        builder.Append(binding.Modifier)
            .Append("partial ")
            .Append(binding.Keyword)
            .Append(' ')
            .Append(binding.Name)
            .AppendLine();
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount, binding.NamespaceName);
        builder.AppendLine("{");

        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 1, binding.NamespaceName);
        builder.Append("public static global::System.Threading.Tasks.ValueTask<")
            .Append(binding.Type)
            .Append("> BindAsync(global::Microsoft.AspNetCore.Http.HttpContext context, global::System.Reflection.ParameterInfo parameter)")
            .AppendLine();
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 1, binding.NamespaceName);
        builder.AppendLine("{");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("global::System.ArgumentNullException.ThrowIfNull(context);");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("var parsingOptions = global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.ResolveQueryParsingOptions(context);");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.Append("var result = global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryStringParser.Parse<")
            .Append(binding.Type)
            .AppendLine(">(context.Request.QueryString.Value, parsingOptions);");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("if (!result.IsSuccess)");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("{");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 3, binding.NamespaceName);
        builder.AppendLine("var message = result.Errors.Count > 0 ? result.Errors[0].Message : \"The query string could not be bound to this request.\";");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 3, binding.NamespaceName);
        builder.AppendLine("throw new global::Microsoft.AspNetCore.Http.BadHttpRequestException(message, global::Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest);");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.AppendLine("}");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 2, binding.NamespaceName);
        builder.Append("return global::System.Threading.Tasks.ValueTask.FromResult<")
            .Append(binding.Type)
            .AppendLine(">(result.Value!);");
        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount + 1, binding.NamespaceName);
        builder.AppendLine("}");

        ValiantSourceBuilder.AppendIndent(builder, containingTypeCount, binding.NamespaceName);
        builder.AppendLine("}");

        ValiantSourceBuilder.AppendContainingTypeClosures(builder, binding.NamespaceName, containingTypeCount);

        if (binding.NamespaceName is not null)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static GroupRegistrationModel[] CreateGroupRegistrations(ImmutableArray<EndpointModel> endpoints)
    {
        var groupOrders = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var endpoint in endpoints)
        {
            if (endpoint.Kind == EndpointKind.Group)
            {
                groupOrders[endpoint.Type] = endpoint.Order;
                continue;
            }

            if (endpoint.GroupType is null || groupOrders.ContainsKey(endpoint.GroupType))
            {
                continue;
            }

            groupOrders[endpoint.GroupType] = endpoint.Order;
        }

        return groupOrders
            .OrderBy(static group => group.Value)
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static (group, index) => new GroupRegistrationModel(
                group.Key,
                group.Value,
                index))
            .ToArray();
    }
}
