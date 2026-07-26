using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using StaticNorth.Valiant.SourceGeneration;

namespace StaticNorth.Valiant.Mapping.SourceGeneration;

/// <summary>
/// Validates mapper facade and factory calls without participating in source-generation discovery.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValiantMapperInvocationAnalyzer : DiagnosticAnalyzer
{
    private const string MappingNamespace = "StaticNorth.Valiant.Mapping";
    private static readonly SymbolDisplayFormat TypeDisplayFormat = ValiantGeneratorTypeDisplayFormats.TypeDisplayFormat;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        ValiantMappingGenerator.MapperFactoryPairNotRegistered,
        ValiantMappingGenerator.MapperFacadePairNotRegistered
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static startContext =>
        {
            var localPairs = new ConcurrentBag<MapperPair>();
            var invocations = new ConcurrentBag<MapperInvocation>();

            startContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeMapperDeclaration(syntaxContext, localPairs),
                SyntaxKind.ClassDeclaration,
                SyntaxKind.RecordDeclaration);
            startContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeInvocation(syntaxContext, invocations),
                SyntaxKind.InvocationExpression);
            startContext.RegisterCompilationEndAction(
                endContext => ReportMissingPairs(endContext, localPairs, invocations));
        });
    }

    private static void AnalyzeMapperDeclaration(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<MapperPair> localPairs)
    {
        if (context.Node is not TypeDeclarationSyntax declaration ||
            !HasPotentialMapperAttribute(declaration) ||
            context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol type ||
            !HasValiantMapperAttribute(type) ||
            !TryGetMapperPair(type, out var sourceType, out var targetType))
        {
            return;
        }

        localPairs.Add(new MapperPair(
            sourceType.ToDisplayString(TypeDisplayFormat),
            targetType.ToDisplayString(TypeDisplayFormat)));
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<MapperInvocation> invocations)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            invocation.Expression is not MemberAccessExpressionSyntax { Name: { } name })
        {
            return;
        }

        var invocationKind = GetPotentialInvocationKind(name, invocation.ArgumentList.Arguments);
        if (invocationKind == MapperInvocationKind.None ||
            context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (invocationKind == MapperInvocationKind.Factory && IsMapperFactoryMethod(method))
        {
            AddFactoryInvocation(context, invocation, method, invocations);
        }
        else if (invocationKind == MapperInvocationKind.Facade && IsMapperFacadeMethod(method))
        {
            AddFacadeInvocation(context, invocation, method, invocations);
        }
    }

    private static MapperInvocationKind GetPotentialInvocationKind(
        SimpleNameSyntax name,
        SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        if (name.Identifier.ValueText == "CreateMapper" &&
            ((name is GenericNameSyntax { TypeArgumentList.Arguments.Count: 2 } && arguments.Count == 0) ||
             (name is IdentifierNameSyntax && arguments.Count == 2 && arguments.All(static argument => argument.Expression is TypeOfExpressionSyntax))))
        {
            return MapperInvocationKind.Factory;
        }

        if (name.Identifier.ValueText == "Map" &&
            ((name is GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } && arguments.Count == 1) ||
             (name is IdentifierNameSyntax && arguments.Count == 2 && arguments[1].Expression is TypeOfExpressionSyntax)))
        {
            return MapperInvocationKind.Facade;
        }

        return MapperInvocationKind.None;
    }

    private static void AddFactoryInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        ConcurrentBag<MapperInvocation> invocations)
    {
        if (method is { IsGenericMethod: true, TypeArguments.Length: 2 })
        {
            invocations.Add(new MapperInvocation(
                MapperInvocationKind.Factory,
                method.TypeArguments[0].ToDisplayString(TypeDisplayFormat),
                method.TypeArguments[1].ToDisplayString(TypeDisplayFormat),
                invocation.GetLocation()));
            return;
        }

        if (invocation.ArgumentList.Arguments.Count == 2 &&
            TryGetTypeOfArgumentType(context, invocation.ArgumentList.Arguments[0], out var sourceType) &&
            TryGetTypeOfArgumentType(context, invocation.ArgumentList.Arguments[1], out var targetType))
        {
            invocations.Add(new MapperInvocation(
                MapperInvocationKind.Factory,
                sourceType.ToDisplayString(TypeDisplayFormat),
                targetType.ToDisplayString(TypeDisplayFormat),
                invocation.GetLocation()));
        }
    }

    private static void AddFacadeInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        ConcurrentBag<MapperInvocation> invocations)
    {
        if (!TryGetUsefulExpressionType(context, invocation.ArgumentList.Arguments[0], out var sourceType))
        {
            return;
        }

        ITypeSymbol? targetType = null;
        if (method is { IsGenericMethod: true, TypeArguments.Length: 1 })
        {
            targetType = method.TypeArguments[0];
        }
        else if (invocation.ArgumentList.Arguments.Count == 2 &&
            TryGetTypeOfArgumentType(context, invocation.ArgumentList.Arguments[1], out var typeOfTarget))
        {
            targetType = typeOfTarget;
        }

        if (targetType is not null)
        {
            invocations.Add(new MapperInvocation(
                MapperInvocationKind.Facade,
                sourceType.ToDisplayString(TypeDisplayFormat),
                targetType.ToDisplayString(TypeDisplayFormat),
                invocation.GetLocation()));
        }
    }

    private static void ReportMissingPairs(
        CompilationAnalysisContext context,
        ConcurrentBag<MapperPair> localPairs,
        ConcurrentBag<MapperInvocation> invocations)
    {
        if (invocations.IsEmpty)
        {
            return;
        }

        var knownPairs = new HashSet<MapperPair>(localPairs);
        AddReferencedMapperPairs(context.Compilation, knownPairs);
        foreach (var invocation in invocations)
        {
            if (knownPairs.Contains(new MapperPair(invocation.SourceType, invocation.TargetType)))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                invocation.Kind == MapperInvocationKind.Factory
                    ? ValiantMappingGenerator.MapperFactoryPairNotRegistered
                    : ValiantMappingGenerator.MapperFacadePairNotRegistered,
                invocation.Location,
                invocation.SourceType,
                invocation.TargetType));
        }
    }

    private static void AddReferencedMapperPairs(Compilation compilation, HashSet<MapperPair> pairs)
    {
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass?.ContainingNamespace.ToDisplayString() != MappingNamespace ||
                    attribute.AttributeClass.MetadataName != "ValiantMapperRegistrationAttribute" ||
                    attribute.ConstructorArguments.Length != 2 ||
                    attribute.ConstructorArguments[0].Value is not ITypeSymbol sourceType ||
                    attribute.ConstructorArguments[1].Value is not ITypeSymbol targetType)
                {
                    continue;
                }

                pairs.Add(new MapperPair(
                    sourceType.ToDisplayString(TypeDisplayFormat),
                    targetType.ToDisplayString(TypeDisplayFormat)));
            }
        }
    }

    private static bool HasPotentialMapperAttribute(TypeDeclarationSyntax declaration)
    {
        foreach (var attribute in declaration.AttributeLists.SelectMany(static list => list.Attributes))
        {
            var identifier = attribute.Name switch
            {
                IdentifierNameSyntax name => name.Identifier.ValueText,
                QualifiedNameSyntax name => name.Right.Identifier.ValueText,
                AliasQualifiedNameSyntax name => name.Name.Identifier.ValueText,
                _ => null
            };

            if (identifier is "ValiantMapper" or "ValiantMapperAttribute")
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasValiantMapperAttribute(INamedTypeSymbol type)
    {
        return type.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ContainingNamespace.ToDisplayString() == MappingNamespace &&
            attribute.AttributeClass.MetadataName == "ValiantMapperAttribute");
    }

    private static bool TryGetMapperPair(
        INamedTypeSymbol type,
        out ITypeSymbol sourceType,
        out ITypeSymbol targetType)
    {
        foreach (var interfaceType in type.AllInterfaces)
        {
            if (interfaceType.ContainingNamespace.ToDisplayString() == MappingNamespace &&
                interfaceType.TypeArguments.Length == 2 &&
                interfaceType.MetadataName is "IMapperDefinition`2" or "IMappingDefinition`2")
            {
                sourceType = interfaceType.TypeArguments[0];
                targetType = interfaceType.TypeArguments[1];
                return true;
            }
        }

        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.ContainingNamespace.ToDisplayString() == MappingNamespace &&
                baseType is { MetadataName: "MapperDefinition`2", TypeArguments.Length: 2 })
            {
                sourceType = baseType.TypeArguments[0];
                targetType = baseType.TypeArguments[1];
                return true;
            }
        }

        sourceType = null!;
        targetType = null!;
        return false;
    }

    private static bool IsMapperFactoryMethod(IMethodSymbol method)
    {
        return ImplementsContract(method.ContainingType, "IMapperFactory");
    }

    private static bool IsMapperFacadeMethod(IMethodSymbol method)
    {
        return ImplementsContract(method.ContainingType, "IMapper");
    }

    private static bool ImplementsContract(INamedTypeSymbol? type, string metadataName)
    {
        if (type is null)
        {
            return false;
        }

        if (type.ContainingNamespace.ToDisplayString() == MappingNamespace && type.MetadataName == metadataName)
        {
            return true;
        }

        return type.AllInterfaces.Any(interfaceType =>
            interfaceType.ContainingNamespace.ToDisplayString() == MappingNamespace &&
            interfaceType.MetadataName == metadataName);
    }

    private static bool TryGetUsefulExpressionType(
        SyntaxNodeAnalysisContext context,
        ArgumentSyntax argument,
        out ITypeSymbol type)
    {
        var expressionType = context.SemanticModel.GetTypeInfo(argument.Expression, context.CancellationToken).Type;
        if (expressionType is not null && expressionType.SpecialType != SpecialType.System_Object)
        {
            type = expressionType;
            return true;
        }

        type = null!;
        return false;
    }

    private static bool TryGetTypeOfArgumentType(
        SyntaxNodeAnalysisContext context,
        ArgumentSyntax argument,
        out ITypeSymbol type)
    {
        if (argument.Expression is TypeOfExpressionSyntax typeOfExpression &&
            context.SemanticModel.GetTypeInfo(typeOfExpression.Type, context.CancellationToken).Type is { } typeSymbol)
        {
            type = typeSymbol;
            return true;
        }

        type = null!;
        return false;
    }

    private enum MapperInvocationKind
    {
        None,
        Factory,
        Facade
    }

    private readonly struct MapperInvocation(
        MapperInvocationKind kind,
        string sourceType,
        string targetType,
        Location location)
    {
        public MapperInvocationKind Kind { get; } = kind;

        public string SourceType { get; } = sourceType;

        public string TargetType { get; } = targetType;

        public Location Location { get; } = location;
    }
}
