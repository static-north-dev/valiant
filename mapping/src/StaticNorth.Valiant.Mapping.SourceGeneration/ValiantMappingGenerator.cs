using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StaticNorth.Valiant.SourceGeneration;
using static StaticNorth.Valiant.SourceGeneration.ValiantNativeControlFlow;
using static StaticNorth.Valiant.SourceGeneration.ValiantTypeDeclarations;

namespace StaticNorth.Valiant.Mapping.SourceGeneration;

/// <summary>
/// Generates Valiant mapper implementations and dependency injection registration code.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class ValiantMappingGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat TypeDisplayFormat = ValiantGeneratorTypeDisplayFormats.TypeDisplayFormat;
    private const string ServiceProviderMapperInvocationPrefix = "MapWithServiceProvider<";

    private static class MappingApi
    {
        public const string Map = "Map";
        public const string Ignore = "Ignore";
        public const string When = "When";
        public const string UseMapper = "UseMapper";
        public const string Default = "Default";
        public const string Fallback = "Fallback";
        public const string IgnoreNull = "IgnoreNull";
        public const string MapNull = "MapNull";
        public const string AllowNull = "AllowNull";
        public const string MappingContextMetadataName = "MappingContext`2";
        public const string MapperDefinitionMetadataName = "MapperDefinition`2";
        public const string MapperInterfaceMetadataName = "IMapper`2";
        public const string MapperDefinitionInterfaceMetadataName = "IMapperDefinition`2";
        public const string MappingDefinitionInterfaceMetadataName = "IMappingDefinition`2";
        public const string MapperAttributeMetadataName = "ValiantMapperAttribute";
        public const string Namespace = "StaticNorth.Valiant.Mapping";
    }

    private static readonly DiagnosticDescriptor MapperMustBePartial = new(
        "VLM001",
        "Mapper must be partial",
        "Mapper '{0}' must be declared partial so Valiant can generate its implementation",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedMapperShape = new(
        "VLM002",
        "Unsupported mapper shape",
        "Mapper '{0}' is not supported: {1}",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MappingPairMissing = new(
        "VLM003",
        "Mapper source and target type pair is missing",
        "Mapper '{0}' must inherit MapperDefinition<TSource, TTarget> or implement IMapperDefinition<TSource, TTarget> so Valiant can determine the source and target types",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedMappingDeclaration = new(
        "VLM004",
        "Unsupported mapping declaration",
        "{0}",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedTargetConstruction = new(
        "VLM005",
        "Target construction is not supported",
        "Mapper '{0}' cannot construct target type '{1}': {2}",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor RequiredTargetMemberNotMapped = new(
        "VLM006",
        "Required target member is not mapped",
        "Required target member '{0}' cannot be mapped from source type '{1}'. Add an explicit Map declaration, Ignore declaration, or fallback/default value.",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor TargetMemberNotMapped = new(
        "VLM101",
        "Target member is not mapped",
        "Target member '{0}' is not mapped from source type '{1}'",
        "Valiant",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor IncompatibleTargetMemberType = new(
        "VLM102",
        "Target member source type is incompatible",
        "Target member '{0}' has source member type '{1}', which cannot be mapped to '{2}'. Add a compatible Map declaration or register a mapper with UseMapper.",
        "Valiant",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor DuplicateMapperPair = new(
        "VLM104",
        "Duplicate mapper pair",
        "Mapper '{0}' maps '{1}' to '{2}', but mapper '{3}' already maps the same source and target types. Only one default mapper per pair is allowed.",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor MapperFactoryPairNotRegistered = new(
        "VLM105",
        "Mapper factory pair is not registered",
        "No Valiant mapper is registered for source type '{0}' and target type '{1}'. Add a Valiant mapper for that pair or use TryCreateMapper.",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor MapperFacadePairNotRegistered = new(
        "VLM106",
        "Mapper facade pair is not registered",
        "No Valiant mapper is registered for source type '{0}' and target type '{1}'. Add a Valiant mapper for that pair or use TryMap.",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var mappers = CreateMapperProvider(
            context,
            MappingApi.Namespace + "." + MappingApi.MapperAttributeMetadataName)
            .WithComparer(MapperModelComparer.Instance);
        var generatedMappers = mappers
            .WithTrackingName("Mapping.Mapper");
        var dependencyInjectionMappers = generatedMappers
            .Where(static mapper => IsValidMapper(mapper))
            .Select(static (mapper, _) => mapper.DependencyInjection)
            .Where(static mapper => mapper is not null)
            .Select(static (mapper, _) => mapper!.Value)
            .Collect();
        var mapperPairs = generatedMappers
            .Where(static mapper => IsValidMapper(mapper))
            .Select(static (mapper, _) => new MapperPairRegistrationModel(mapper.Mapping!.SourceType, mapper.Mapping.TargetType))
            .Collect()
            .WithTrackingName("Mapping.MapperPairs");
        var hasDependencyInjection = context.CompilationProvider
            .Select(static (compilation, _) => ValiantDependencyInjectionSymbols.HasDependencyInjection(compilation));

        context.RegisterSourceOutput(mappers, static (context, mapper) => ExecuteMapperDiagnostics(context, mapper));
        context.RegisterSourceOutput(generatedMappers, static (context, mapper) => ExecuteMapperSource(context, mapper));
        context.RegisterSourceOutput(
            mappers.Collect(),
            static (context, mappers) => ExecuteDuplicateMapperPairDiagnostics(context, mappers));
        context.RegisterSourceOutput(
            mapperPairs,
            static (context, pairs) => ExecuteMapperPairRegistrationSource(context, pairs));
        context.RegisterSourceOutput(
            hasDependencyInjection
                .Combine(dependencyInjectionMappers)
                .WithTrackingName("Mapping.DependencyInjection"),
            static (context, source) => ExecuteDependencyInjection(context, source.Left, source.Right));
    }

    private static IncrementalValuesProvider<MapperModel> CreateMapperProvider(
        IncrementalGeneratorInitializationContext context,
        string attributeMetadataName)
    {
        return context.SyntaxProvider
            .ForAttributeWithMetadataName(
                attributeMetadataName,
                static (node, _) => IsMapperCandidateSyntax(node),
                static (context, cancellationToken) => CreateMapperModel(context, cancellationToken))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);
    }

    private static bool IsMapperCandidateSyntax(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { AttributeLists.Count: > 0 } ||
            node is RecordDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    private static MapperModel? CreateMapperModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var typeSyntax = (TypeDeclarationSyntax)context.TargetNode;
        var mapperSymbol = (INamedTypeSymbol)context.TargetSymbol;
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        var attribute = context.Attributes.FirstOrDefault(IsValiantMapperAttribute);
        if (attribute is null)
        {
            return null;
        }

        if (!typeSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            diagnostics.Add(new DiagnosticInfo(
                MapperMustBePartial,
                typeSyntax.Identifier.GetLocation(),
                mapperSymbol.Name));
        }

        if (mapperSymbol.TypeKind != TypeKind.Class)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMapperShape,
                typeSyntax.Identifier.GetLocation(),
                mapperSymbol.Name,
                "mappers must be classes or record classes"));
        }

        if (mapperSymbol.IsGenericType)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMapperShape,
                typeSyntax.Identifier.GetLocation(),
                mapperSymbol.Name,
                "generic mapper types are not supported yet"));
        }

        if (mapperSymbol.IsAbstract || mapperSymbol.IsStatic)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMapperShape,
                typeSyntax.Identifier.GetLocation(),
                mapperSymbol.Name,
                "mappers must be concrete instance types"));
        }

        for (var containingType = mapperSymbol.ContainingType; containingType is not null; containingType = containingType.ContainingType)
        {
            if (containingType.IsGenericType)
            {
                diagnostics.Add(new DiagnosticInfo(
                    UnsupportedMapperShape,
                    typeSyntax.Identifier.GetLocation(),
                    mapperSymbol.Name,
                    "nested mappers inside generic containing types are not supported yet"));
                break;
            }
        }

        for (SyntaxNode? parent = typeSyntax.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is TypeDeclarationSyntax containingTypeSyntax &&
                !containingTypeSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                diagnostics.Add(new DiagnosticInfo(
                    UnsupportedMapperShape,
                    containingTypeSyntax.Identifier.GetLocation(),
                    mapperSymbol.Name,
                    "containing types for nested mappers must be partial"));
            }
        }

        var declaration = CreateMapperDeclarationModel(mapperSymbol, typeSyntax.Identifier.GetLocation());
        var definitionPair = GetMappingDefinitionPair(mapperSymbol);
        var basePair = GetMapperBasePair(mapperSymbol);
        var inheritsMapperBase = basePair is not null;
        var mappingPair = definitionPair ?? basePair;
        MappingModel? mapping = null;
        DependencyInjectionMapperModel? dependencyInjection = null;

        if (mappingPair is null)
        {
            diagnostics.Add(new DiagnosticInfo(
                MappingPairMissing,
                typeSyntax.Identifier.GetLocation(),
                mapperSymbol.Name));
        }
        else
        {
            if (definitionPair is not null &&
                basePair is not null &&
                (!SymbolEqualityComparer.Default.Equals(definitionPair.SourceType, basePair.SourceType) ||
                !SymbolEqualityComparer.Default.Equals(definitionPair.TargetType, basePair.TargetType)))
            {
                diagnostics.Add(new DiagnosticInfo(
                    UnsupportedMapperShape,
                    typeSyntax.Identifier.GetLocation(),
                    mapperSymbol.Name,
                    "MapperDefinition<TSource, TTarget> and IMapperDefinition<TSource, TTarget> must declare the same source and target types"));
            }

            var configureMethod = FindConfigureMethod(mapperSymbol, cancellationToken);
            var configure = ConfigureModel.Empty;
            if (configureMethod is not null)
            {
                configure = CreateConfigureModel(
                    configureMethod,
                    mappingPair,
                    context.SemanticModel.Compilation,
                    diagnostics,
                    cancellationToken);
            }

            mapping = CreateMappingModel(
                mapperSymbol,
                mappingPair,
                inheritsMapperBase,
                configureMethod is not null,
                configure,
                context.SemanticModel.Compilation,
                typeSyntax.Identifier.GetLocation(),
                diagnostics,
                cancellationToken);

            if (mapping is not null)
            {
                dependencyInjection = CreateDependencyInjectionMapperModel(mapperSymbol, mappingPair);
            }
        }

        var finalDiagnostics = diagnostics.ToImmutable();
        return new MapperModel(
            declaration,
            mapping,
            dependencyInjection,
            finalDiagnostics,
            CreateMapperFingerprint(declaration, mapping, finalDiagnostics));
    }

    private static bool IsValiantMapperAttribute(AttributeData attribute)
    {
        var attributeClass = attribute.AttributeClass;
        if (attributeClass is null)
        {
            return false;
        }

        return attributeClass.ContainingNamespace.ToDisplayString() == MappingApi.Namespace &&
            attributeClass.MetadataName == MappingApi.MapperAttributeMetadataName;
    }

    private static DependencyInjectionMapperModel CreateDependencyInjectionMapperModel(
        INamedTypeSymbol mapperSymbol,
        MappingPair mappingPair)
    {
        return new DependencyInjectionMapperModel(
            mapperSymbol.ToDisplayString(TypeDisplayFormat),
            mappingPair.SourceType.ToDisplayString(TypeDisplayFormat),
            mappingPair.TargetType.ToDisplayString(TypeDisplayFormat));
    }

    private static MappingPair? GetMappingDefinitionPair(INamedTypeSymbol mapperSymbol)
    {
        foreach (var interfaceType in mapperSymbol.AllInterfaces)
        {
            if (interfaceType.ContainingNamespace.ToDisplayString() == MappingApi.Namespace &&
                interfaceType.TypeArguments.Length == 2 &&
                (interfaceType.MetadataName == MappingApi.MapperDefinitionInterfaceMetadataName ||
                    interfaceType.MetadataName == MappingApi.MappingDefinitionInterfaceMetadataName))
            {
                return new MappingPair(interfaceType.TypeArguments[0], interfaceType.TypeArguments[1]);
            }
        }

        return null;
    }

    private static MappingPair? GetMapperBasePair(INamedTypeSymbol mapperSymbol)
    {
        for (var baseType = mapperSymbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.ContainingNamespace.ToDisplayString() == MappingApi.Namespace &&
                baseType is { MetadataName: MappingApi.MapperDefinitionMetadataName, TypeArguments.Length: 2 })
            {
                return new MappingPair(baseType.TypeArguments[0], baseType.TypeArguments[1]);
            }
        }

        return null;
    }

    private static ConfigureModel CreateConfigureModel(
        MethodDeclarationSyntax methodSyntax,
        MappingPair mappingPair,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        var explicitMappings = ImmutableArray.CreateBuilder<ExplicitMemberMappingModel>();
        var configureLocals = ImmutableArray.CreateBuilder<string>();
        var ignoredMappings = ImmutableArray.CreateBuilder<IgnoredMemberMappingModel>();
        var mapperReuses = ImmutableArray.CreateBuilder<MapperReuseModel>();

        if (!methodSyntax.Modifiers.Any(SyntaxKind.PublicKeyword))
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMappingDeclaration,
                methodSyntax.Identifier.GetLocation(),
                "Configure method must be public."));
        }

        if (methodSyntax.ParameterList.Parameters.Count != 1)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMappingDeclaration,
                methodSyntax.Identifier.GetLocation(),
                "Configure method must have exactly one parameter."));
            return ConfigureModel.ConventionDisabled;
        }

        var semanticModel = compilation.GetSemanticModel(methodSyntax.SyntaxTree);
        if (semanticModel.GetDeclaredSymbol(methodSyntax, cancellationToken) is not { } methodSymbol)
        {
            return ConfigureModel.ConventionDisabled;
        }

        if (!methodSymbol.ReturnsVoid)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMappingDeclaration,
                methodSyntax.Identifier.GetLocation(),
                "Configure method must return void."));
        }

        var parameterType = methodSymbol.Parameters[0].Type;
        if (!IsMappingContextType(parameterType, mappingPair))
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMappingDeclaration,
                methodSyntax.ParameterList.Parameters[0].GetLocation(),
                "Configure method parameter must be MappingContext<TSource, TTarget>."));
        }

        if (methodSyntax.Body is null)
        {
            return ConfigureModel.ConventionDisabled;
        }

        var contextParameterName = methodSymbol.Parameters[0].Name;
        ParseConfigureStatements(
            methodSyntax.Body.Statements,
            contextParameterName,
            null,
            semanticModel,
            cancellationToken,
            explicitMappings,
            configureLocals,
            ignoredMappings,
            mapperReuses,
            diagnostics);

        return new ConfigureModel(
            true,
            explicitMappings.ToImmutable(),
            ignoredMappings.ToImmutable(),
            mapperReuses.ToImmutable(),
            configureLocals.ToImmutable());
    }

    private static void ParseConfigureStatements(
        SyntaxList<StatementSyntax> statements,
        string contextParameterName,
        string? conditionExpression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ExplicitMemberMappingModel>.Builder explicitMappings,
        ImmutableArray<string>.Builder configureLocals,
        ImmutableArray<IgnoredMemberMappingModel>.Builder ignoredMappings,
        ImmutableArray<MapperReuseModel>.Builder mapperReuses,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        foreach (var statement in statements)
        {
            ParseConfigureStatement(
                statement,
                contextParameterName,
                conditionExpression,
                semanticModel,
                cancellationToken,
                explicitMappings,
                configureLocals,
                ignoredMappings,
                mapperReuses,
                diagnostics);
        }
    }

    private static void ParseConfigureStatement(
        StatementSyntax statement,
        string contextParameterName,
        string? conditionExpression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ExplicitMemberMappingModel>.Builder explicitMappings,
        ImmutableArray<string>.Builder configureLocals,
        ImmutableArray<IgnoredMemberMappingModel>.Builder ignoredMappings,
        ImmutableArray<MapperReuseModel>.Builder mapperReuses,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (statement)
        {
            case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }:
                if (TryCreateConfigureDeclarationChain(
                    invocation,
                    contextParameterName,
                    conditionExpression,
                    semanticModel,
                    explicitMappings,
                    configureLocals,
                    ignoredMappings,
                    mapperReuses,
                    diagnostics))
                {
                    return;
                }

                diagnostics.Add(new DiagnosticInfo(
                    UnsupportedMappingDeclaration,
                    invocation.GetLocation(),
                    "Only Map, Ignore, UseMapper, Default, Fallback, IgnoreNull, MapNull, blocks, and if/else statements are supported in mapper Configure methods."));
                return;

            case BlockSyntax block:
                ParseConfigureStatements(block.Statements, contextParameterName, conditionExpression, semanticModel, cancellationToken, explicitMappings, configureLocals, ignoredMappings, mapperReuses, diagnostics);
                return;

            case IfStatementSyntax ifStatement:
                var branchExpression = RewriteMappingCondition(ifStatement.Condition, contextParameterName);
                var condition = "__valiantBranch" + configureLocals.Count.ToString(CultureInfo.InvariantCulture);
                var activeCondition = CombineConditions(conditionExpression, branchExpression);
                configureLocals.Add("var " + condition + " = " + activeCondition + ";");
                ParseConfigureStatement(
                    ifStatement.Statement,
                    contextParameterName,
                    condition,
                    semanticModel,
                    cancellationToken,
                    explicitMappings,
                    configureLocals,
                    ignoredMappings,
                    mapperReuses,
                    diagnostics);
                if (ifStatement.Else is not null)
                {
                    ParseConfigureStatement(
                        ifStatement.Else.Statement,
                        contextParameterName,
                        CombineConditions(conditionExpression, NegateCondition(condition)),
                        semanticModel,
                        cancellationToken,
                        explicitMappings,
                        configureLocals,
                        ignoredMappings,
                        mapperReuses,
                        diagnostics);
                }

                return;

            default:
                diagnostics.Add(new DiagnosticInfo(
                    UnsupportedMappingDeclaration,
                    statement.GetLocation(),
                    "Only Map, Ignore, UseMapper, Default, Fallback, IgnoreNull, MapNull, blocks, and if/else statements are supported in mapper Configure methods."));
                return;
        }
    }

    private static bool TryCreateConfigureDeclarationChain(
        InvocationExpressionSyntax invocation,
        string contextParameterName,
        string? branchConditionExpression,
        SemanticModel semanticModel,
        ImmutableArray<ExplicitMemberMappingModel>.Builder explicitMappings,
        ImmutableArray<string>.Builder configureLocals,
        ImmutableArray<IgnoredMemberMappingModel>.Builder ignoredMappings,
        ImmutableArray<MapperReuseModel>.Builder mapperReuses,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var invocations = GetInvocationChain(invocation);
        if (invocations.Count == 0 ||
            !IsContextInvocation(invocations[0], contextParameterName))
        {
            return false;
        }

        var pendingExplicitMappings = new List<PendingExplicitMappingModel>();
        var pendingIgnoredMappings = new List<(string TargetMemberName, Location Location)>();
        string? chainConditionExpression = null;

        foreach (var currentInvocation in invocations)
        {
            var methodName = GetInvocationName(currentInvocation);
            switch (methodName)
            {
                case MappingApi.Map:
                    if (!TryReadMapDeclaration(
                        currentInvocation,
                        semanticModel,
                        diagnostics,
                        out var targetMemberName,
                        out var sourceExpression,
                        out var sourceType,
                        out var location))
                    {
                        return true;
                    }

                    pendingExplicitMappings.Add(new PendingExplicitMappingModel(targetMemberName, sourceExpression, sourceType, location));
                    break;

                case MappingApi.Ignore:
                    if (!TryReadIgnoreDeclaration(
                        currentInvocation,
                        semanticModel,
                        diagnostics,
                        out var ignoredTargetMemberName,
                        out var ignoredLocation))
                    {
                        return true;
                    }

                    pendingIgnoredMappings.Add((ignoredTargetMemberName, ignoredLocation));
                    break;

                case MappingApi.When:
                    if (pendingExplicitMappings.Count == 0 &&
                        pendingIgnoredMappings.Count == 0)
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            UnsupportedMappingDeclaration,
                            currentInvocation.GetLocation(),
                            "When must be chained after Map or Ignore."));
                        return true;
                    }

                    if (currentInvocation.ArgumentList.Arguments.Count != 1 ||
                        !TryCreateSourcePredicateExpression(currentInvocation.ArgumentList.Arguments[0].Expression, semanticModel, out var conditionExpression))
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            UnsupportedMappingDeclaration,
                            currentInvocation.GetLocation(),
                            "When expects one source predicate."));
                        return true;
                    }

                    chainConditionExpression = CombineConditions(chainConditionExpression, conditionExpression);
                    break;

                case MappingApi.Default:
                case MappingApi.Fallback:
                    if (pendingExplicitMappings.Count == 0)
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            UnsupportedMappingDeclaration,
                            currentInvocation.GetLocation(),
                            methodName + " must be chained after Map."));
                        return true;
                    }

                    if (currentInvocation.ArgumentList.Arguments.Count != 1)
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            UnsupportedMappingDeclaration,
                            currentInvocation.GetLocation(),
                            methodName + " expects one fallback value."));
                        return true;
                    }

                    var fallbackArgument = currentInvocation.ArgumentList.Arguments[0].Expression;
                    var fallbackType = semanticModel.GetTypeInfo(fallbackArgument).ConvertedType;
                    if (fallbackType is null)
                    {
                        return true;
                    }

                    var fallbackLocal = "__valiantFallback" + configureLocals.Count.ToString(CultureInfo.InvariantCulture);
                    var fallbackValue = RewriteMappingCondition(fallbackArgument, contextParameterName);
                    var fallbackDeclaration = fallbackType.ToDisplayString(TypeDisplayFormat) + " " + fallbackLocal + " = ";
                    configureLocals.Add(fallbackDeclaration + (branchConditionExpression is null
                        ? fallbackValue
                        : "(" + branchConditionExpression + ") ? (" + fallbackValue + ") : default!") + ";");
                    pendingExplicitMappings[pendingExplicitMappings.Count - 1].FallbackExpression = fallbackLocal;
                    break;

                case MappingApi.IgnoreNull:
                case MappingApi.MapNull:
                case MappingApi.AllowNull:
                    if (pendingExplicitMappings.Count == 0)
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            UnsupportedMappingDeclaration,
                            currentInvocation.GetLocation(),
                            methodName + " must be chained after Map."));
                        return true;
                    }

                    if (currentInvocation.ArgumentList.Arguments.Count != 0)
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            UnsupportedMappingDeclaration,
                            currentInvocation.GetLocation(),
                            methodName + " does not accept arguments."));
                        return true;
                    }

                    pendingExplicitMappings[pendingExplicitMappings.Count - 1].NullHandling =
                        methodName == MappingApi.IgnoreNull
                            ? NullHandlingPolicy.IgnoreSourceNulls
                            : NullHandlingPolicy.MapNulls;
                    break;

                case MappingApi.UseMapper:
                    AddPendingMappingDeclarations();
                    if (!TryReadMapperReuseDeclaration(currentInvocation, semanticModel, diagnostics, out var mapperReuse))
                    {
                        return true;
                    }

                    var mapperLocal = "__valiantMapper" + configureLocals.Count.ToString(CultureInfo.InvariantCulture);
                    var mapperExpression = RewriteMappingCondition(currentInvocation.ArgumentList.Arguments[0].Expression, contextParameterName);
                    var mapperType = GetMapperInterfaceDisplayName(mapperReuse.SourceType, mapperReuse.TargetType);
                    var capture = "(" + mapperType + "?)(" + mapperExpression + ") ?? throw new global::System.ArgumentNullException(\"mapper\")";
                    if (branchConditionExpression is not null)
                    {
                        capture = "(" + branchConditionExpression + ") ? (" + capture + ") : null";
                    }

                    configureLocals.Add("var " + mapperLocal + " = " + capture + ";");
                    mapperReuses.Add(new MapperReuseModel(mapperLocal, mapperReuse.SourceType, mapperReuse.TargetType, branchConditionExpression));
                    break;

                default:
                    diagnostics.Add(new DiagnosticInfo(
                            UnsupportedMappingDeclaration,
                            currentInvocation.GetLocation(),
                            "Only Map, Ignore, UseMapper, Default, Fallback, IgnoreNull, MapNull, and chained When calls are supported in mapper Configure expression chains."));
                    return true;
            }
        }

        AddPendingMappingDeclarations();
        return true;

        void AddPendingMappingDeclarations()
        {
            foreach (var pendingMapping in pendingExplicitMappings)
            {
                explicitMappings.Add(new ExplicitMemberMappingModel(
                    pendingMapping.TargetMemberName,
                    pendingMapping.SourceExpression,
                    pendingMapping.SourceType,
                    pendingMapping.FallbackExpression,
                    pendingMapping.NullHandling,
                    branchConditionExpression,
                    chainConditionExpression,
                    pendingMapping.Location,
                    explicitMappings.Count));
            }

            foreach (var pendingIgnoredMapping in pendingIgnoredMappings)
            {
                ignoredMappings.Add(new IgnoredMemberMappingModel(
                    pendingIgnoredMapping.TargetMemberName,
                    CombineConditions(branchConditionExpression, chainConditionExpression),
                    pendingIgnoredMapping.Location));
            }

            pendingExplicitMappings.Clear();
            pendingIgnoredMappings.Clear();
            chainConditionExpression = null;
        }
    }

    private static List<InvocationExpressionSyntax> GetInvocationChain(InvocationExpressionSyntax invocation)
    {
        var invocations = new List<InvocationExpressionSyntax>();
        for (var currentInvocation = invocation; currentInvocation is not null;)
        {
            invocations.Add(currentInvocation);
            currentInvocation = currentInvocation.Expression is MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax innerInvocation }
                ? innerInvocation
                : null;
        }

        invocations.Reverse();
        return invocations;
    }

    private static bool TryReadMapDeclaration(
        InvocationExpressionSyntax mapInvocation,
        SemanticModel semanticModel,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        out string targetMemberName,
        out string sourceExpression,
        out ITypeSymbol sourceType,
        out Location location)
    {
        targetMemberName = string.Empty;
        sourceExpression = string.Empty;
        sourceType = null!;
        location = mapInvocation.GetLocation();
        if (mapInvocation.ArgumentList.Arguments.Count != 2)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMappingDeclaration,
                mapInvocation.GetLocation(),
                "Map expects source and target member selectors."));
            return false;
        }

        var sourceSelector = mapInvocation.ArgumentList.Arguments[0].Expression;
        var targetSelector = mapInvocation.ArgumentList.Arguments[1].Expression;
        if (!TryCreateSourceSelectorExpression(sourceSelector, semanticModel, out sourceExpression, out sourceType))
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMappingDeclaration,
                sourceSelector.GetLocation(),
                "Map source selector must be a single-parameter expression."));
            return false;
        }

        if (!TryGetTargetMemberName(targetSelector, semanticModel, out targetMemberName))
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMappingDeclaration,
                targetSelector.GetLocation(),
                "Map target selector must select a target member."));
            return false;
        }

        return true;
    }

    private static bool TryReadIgnoreDeclaration(
        InvocationExpressionSyntax ignoreInvocation,
        SemanticModel semanticModel,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        out string targetMemberName,
        out Location location)
    {
        targetMemberName = string.Empty;
        location = ignoreInvocation.GetLocation();
        if (ignoreInvocation.ArgumentList.Arguments.Count != 1)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMappingDeclaration,
                ignoreInvocation.GetLocation(),
                "Ignore expects one target member selector."));
            return false;
        }

        var targetSelector = ignoreInvocation.ArgumentList.Arguments[0].Expression;
        if (!TryGetTargetMemberName(targetSelector, semanticModel, out targetMemberName))
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMappingDeclaration,
                targetSelector.GetLocation(),
                "Ignore target selector must select a target member."));
            return false;
        }

        return true;
    }

    private static bool TryReadMapperReuseDeclaration(
        InvocationExpressionSyntax useMapperInvocation,
        SemanticModel semanticModel,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        out MapperReuseModel mapperReuse)
    {
        mapperReuse = null!;
        if (useMapperInvocation.ArgumentList.Arguments.Count != 1)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMappingDeclaration,
                useMapperInvocation.GetLocation(),
                "UseMapper expects one mapper expression."));
            return false;
        }

        var mapperExpression = useMapperInvocation.ArgumentList.Arguments[0].Expression;
        var mapperType = semanticModel.GetTypeInfo(mapperExpression).Type;
        if (mapperType is null ||
            !TryGetMapperReusePair(mapperType, out var sourceType, out var targetType))
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedMappingDeclaration,
                mapperExpression.GetLocation(),
                "UseMapper expects a mapper definition or IMapper<TSource, TTarget> expression."));
            return false;
        }

        mapperReuse = new MapperReuseModel(
            mapperExpression.ToString(),
            sourceType,
            targetType);
        return true;
    }

    private static MethodDeclarationSyntax? FindConfigureMethod(INamedTypeSymbol mapperSymbol, CancellationToken cancellationToken)
    {
        foreach (var reference in mapperSymbol.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reference.GetSyntax(cancellationToken) is not TypeDeclarationSyntax typeDeclaration)
            {
                continue;
            }

            foreach (var method in typeDeclaration.Members.OfType<MethodDeclarationSyntax>())
            {
                if (method.Identifier.ValueText == "Configure")
                {
                    return method;
                }
            }
        }

        return null;
    }

    private static bool IsMappingContextType(ITypeSymbol type, MappingPair mappingPair)
    {
        return type is INamedTypeSymbol
        {
            ContainingNamespace: { } containingNamespace,
            MetadataName: MappingApi.MappingContextMetadataName,
            TypeArguments.Length: 2
        } namedType &&
            containingNamespace.ToDisplayString() == MappingApi.Namespace &&
            SymbolEqualityComparer.Default.Equals(namedType.TypeArguments[0], mappingPair.SourceType) &&
            SymbolEqualityComparer.Default.Equals(namedType.TypeArguments[1], mappingPair.TargetType);
    }

    private static string? GetInvocationName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name }
            ? name
            : null;
    }

    private static bool IsContextInvocation(InvocationExpressionSyntax invocation, string contextParameterName)
    {
        return invocation.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver } &&
            string.Equals(receiver.Identifier.ValueText, contextParameterName, StringComparison.Ordinal);
    }

    private static bool TryCreateSourceSelectorExpression(
        ExpressionSyntax selector,
        SemanticModel semanticModel,
        out string expression,
        out ITypeSymbol sourceType)
    {
        expression = string.Empty;
        sourceType = null!;
        if (!TryGetLambdaParts(selector, out var parameterName, out var body))
        {
            return false;
        }

        var type = semanticModel.GetTypeInfo(body).Type;
        if (type is null)
        {
            return false;
        }

        expression = RewriteLambdaBody(body, parameterName, "source");
        sourceType = type;
        return true;
    }

    private static bool TryCreateSourcePredicateExpression(
        ExpressionSyntax selector,
        SemanticModel semanticModel,
        out string expression)
    {
        expression = string.Empty;
        if (!TryGetLambdaParts(selector, out var parameterName, out var body))
        {
            return false;
        }

        var type = semanticModel.GetTypeInfo(body).Type;
        if (type?.SpecialType != SpecialType.System_Boolean)
        {
            return false;
        }

        expression = RewriteLambdaBody(body, parameterName, "source");
        return true;
    }

    private static bool TryGetTargetMemberName(
        ExpressionSyntax selector,
        SemanticModel semanticModel,
        out string targetMemberName)
    {
        targetMemberName = string.Empty;
        if (!TryGetLambdaParts(selector, out _, out var body))
        {
            return false;
        }

        var symbol = semanticModel.GetSymbolInfo(body).Symbol;
        switch (symbol)
        {
            case IPropertySymbol { IsStatic: false } property:
                targetMemberName = property.Name;
                return true;
            case IFieldSymbol { IsStatic: false } field:
                targetMemberName = field.Name;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetLambdaParts(ExpressionSyntax expression, out string parameterName, out ExpressionSyntax body)
    {
        parameterName = string.Empty;
        body = null!;
        switch (expression)
        {
            case SimpleLambdaExpressionSyntax { Body: ExpressionSyntax simpleBody } simpleLambda:
                parameterName = simpleLambda.Parameter.Identifier.ValueText;
                body = simpleBody;
                return true;
            case ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1, Body: ExpressionSyntax parenthesizedBody } parenthesizedLambda:
                parameterName = parenthesizedLambda.ParameterList.Parameters[0].Identifier.ValueText;
                body = parenthesizedBody;
                return true;
            default:
                return false;
        }
    }

    private static string RewriteLambdaBody(ExpressionSyntax expression, string parameterName, string replacementName)
    {
        return new LambdaParameterRewriter(parameterName, replacementName)
            .Visit(expression)
            .ToString();
    }

    private static string RewriteMappingCondition(ExpressionSyntax expression, string contextParameterName)
    {
        return new MappingContextSourceRewriter(contextParameterName)
            .Visit(expression)
            .ToString();
    }

    private static bool TryGetMapperReusePair(ITypeSymbol mapperType, out ITypeSymbol sourceType, out ITypeSymbol targetType)
    {
        sourceType = null!;
        targetType = null!;
        if (mapperType is INamedTypeSymbol namedMapperType)
        {
            if (namedMapperType.ContainingNamespace.ToDisplayString() == MappingApi.Namespace &&
                namedMapperType is { MetadataName: MappingApi.MapperInterfaceMetadataName, TypeArguments.Length: 2 })
            {
                sourceType = namedMapperType.TypeArguments[0];
                targetType = namedMapperType.TypeArguments[1];
                return true;
            }

            var definitionPair = GetMappingDefinitionPair(namedMapperType) ?? GetMapperBasePair(namedMapperType);
            if (definitionPair is not null)
            {
                sourceType = definitionPair.SourceType;
                targetType = definitionPair.TargetType;
                return true;
            }
        }

        foreach (var interfaceType in mapperType.AllInterfaces)
        {
            if (interfaceType.ContainingNamespace.ToDisplayString() != MappingApi.Namespace ||
                interfaceType is not { MetadataName: MappingApi.MapperInterfaceMetadataName, TypeArguments.Length: 2 })
            {
                continue;
            }
            
            sourceType = interfaceType.TypeArguments[0];
            targetType = interfaceType.TypeArguments[1];
            return true;
        }

        return false;
    }

    private static MappingModel? CreateMappingModel(
        INamedTypeSymbol mapperSymbol,
        MappingPair mappingPair,
        bool inheritsMapperBase,
        bool hasConfigureMethod,
        ConfigureModel configure,
        Compilation compilation,
        Location location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sourceMembers = GetSourceMembers(mappingPair.SourceType);
        var targetMemberCache = new Dictionary<ITypeSymbol, ImmutableArray<TargetMemberModel>>(SymbolEqualityComparer.Default);
        var targetMembers = GetTargetMembers(mappingPair.TargetType, targetMemberCache);
        var constructor = CreateConstructorMapping(
            mapperSymbol,
            mappingPair,
            sourceMembers,
            targetMemberCache,
            configure,
            compilation,
            location,
            diagnostics);

        if (constructor is null)
        {
            AddUnmappedTargetMemberDiagnostics(
                mappingPair,
                targetMembers,
                sourceMembers,
                targetMemberCache,
                configure,
                compilation,
                location,
                diagnostics);
            return null;
        }

        var constructorMemberNames = constructor.Arguments
            .Select(static argument => argument.ParameterName)
            .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
        var assignments = ImmutableArray.CreateBuilder<MemberAssignmentModel>();
        var collectionMappings = ImmutableArray.CreateBuilder<CollectionMappingModel>();
        collectionMappings.AddRange(constructor.CollectionMappings);
        var memberSourceIndex = 0;

        foreach (var targetMember in targetMembers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (constructorMemberNames.Contains(targetMember.Name))
            {
                continue;
            }

            if (HasUnconditionalIgnore(configure.IgnoredMappings, targetMember.Name))
            {
                continue;
            }

            var explicitMappings = GetExplicitMappings(configure.ExplicitMappings, targetMember.Name);
            if (explicitMappings.Length > 0)
            {
                var successfulMappingConditions = ImmutableArray.CreateBuilder<string?>();
                foreach (var explicitMapping in explicitMappings)
                {
                    var userMappingCondition = CombineConditions(explicitMapping.BranchConditionExpression, explicitMapping.ConditionExpression);
                    if (userMappingCondition is not null && !targetMember.CanAssignAfterConstruction)
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            UnsupportedMappingDeclaration,
                            explicitMapping.Location ?? targetMember.Location ?? location,
                            "Conditional Map declarations cannot target init-only members."));
                        continue;
                    }

                    var explicitSource = explicitMapping.SourceExpression;
                    var nullGuardCondition = CaptureNullGuardedSource(
                        ref explicitSource, explicitMapping.SourceType, explicitMapping.NullHandling,
                        "__valiantMemberSource" + memberSourceIndex++.ToString(CultureInfo.InvariantCulture));
                    if (!TryCreateMappedExpression(
                        explicitSource,
                        explicitMapping.SourceType,
                        targetMember.Type,
                        configure.MapperReuses,
                        compilation,
                        out var explicitMappedExpression))
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            IncompatibleTargetMemberType,
                            targetMember.Location ?? explicitMapping.Location ?? location,
                            targetMember.Name,
                            explicitMapping.SourceType.ToDisplayString(TypeDisplayFormat),
                            targetMember.Type.ToDisplayString(TypeDisplayFormat)));
                        continue;
                    }

                    var ignoreGuardCondition = CreateIgnoreGuardCondition(configure.IgnoredMappings, targetMember.Name);
                    var assignmentCondition = CombineConditions(explicitMapping.ConditionExpression, nullGuardCondition);
                    var fallbackExpression = explicitMapping.FallbackExpression;
                    var explicitSourceExpression = explicitMappedExpression.Expression;
                    var finalSourceExpression = explicitSourceExpression;
                    var finalConditionExpression = CombineConditions(ignoreGuardCondition, CombineConditions(explicitMapping.BranchConditionExpression, assignmentCondition));
                    var coverageCondition = CombineConditions(userMappingCondition, ignoreGuardCondition);
                    if (fallbackExpression is not null)
                    {
                        finalSourceExpression = assignmentCondition is null
                            ? explicitSourceExpression
                            : "(" + assignmentCondition + ") ? " + explicitSourceExpression + " : " + fallbackExpression;
                        finalConditionExpression = CombineConditions(ignoreGuardCondition, explicitMapping.BranchConditionExpression);
                        coverageCondition = finalConditionExpression;
                    }
                    var useInitSetter = !targetMember.CanAssignAfterConstruction && ignoreGuardCondition is not null && !targetMember.IsRequired;
                    if (!targetMember.CanAssignAfterConstruction && !useInitSetter && finalConditionExpression is not null)
                    {
                        finalSourceExpression = "(" + finalConditionExpression + ") ? " + explicitSourceExpression + " : default!";
                        finalConditionExpression = null;
                    }

                    assignments.Add(new MemberAssignmentModel(
                        targetMember.Name,
                        finalSourceExpression,
                        finalConditionExpression,
                        explicitMapping.DeclarationOrder,
                        useInitializer: (!targetMember.CanAssignAfterConstruction && !useInitSetter) || targetMember.IsRequired,
                        initPropertyType: useInitSetter ? targetMember.Type.ToDisplayString(TypeDisplayFormat) : null));
                    collectionMappings.AddRange(explicitMappedExpression.CollectionMappings);
                    successfulMappingConditions.Add(coverageCondition);
                }

                var fullyMappedWhenNotIgnored = !targetMember.IsRequired &&
                    HasConditionalIgnore(configure.IgnoredMappings, targetMember.Name) &&
                    ConditionsCoverAllPaths([.. explicitMappings.Select(static mapping => mapping.FallbackExpression is not null
                        ? mapping.BranchConditionExpression
                        : CombineConditions(mapping.BranchConditionExpression, mapping.ConditionExpression))]);
                if (!fullyMappedWhenNotIgnored &&
                    !ConditionsCoverAllPaths(successfulMappingConditions.ToImmutable()))
                {
                    AddUnmappedTargetMemberDiagnostic(mappingPair, targetMember, location, diagnostics);
                }

                continue;
            }

            if (configure.DisablesConventionMapping)
            {
                AddUnmappedTargetMemberDiagnostic(mappingPair, targetMember, location, diagnostics);
                continue;
            }

            if (!TryGetSourceMember(sourceMembers, targetMember.Name, out var sourceMember) &&
                !TryGetFlattenedSourceMember(sourceMembers, targetMember.Name, out sourceMember))
            {
                if (!configure.DisablesConventionMapping &&
                    TryCreateUnflattenedExpression(
                        targetMember.Name,
                        targetMember.Type,
                        sourceMembers,
                        targetMemberCache,
                        configure.MapperReuses,
                        compilation,
                        out var unflattenedMappedExpression))
                {
                    assignments.Add(new MemberAssignmentModel(targetMember.Name, unflattenedMappedExpression.Expression, null));
                    collectionMappings.AddRange(unflattenedMappedExpression.CollectionMappings);
                    continue;
                }

                AddUnmappedTargetMemberDiagnostic(mappingPair, targetMember, location, diagnostics);
                continue;
            }

            var memberSource = sourceMember.Expression;
            var memberNullGuardCondition = CaptureNullGuardedSource(
                ref memberSource, sourceMember.Type, NullHandlingPolicy.Default,
                "__valiantMemberSource" + memberSourceIndex++.ToString(CultureInfo.InvariantCulture));
            if (!TryCreateMappedExpression(
                memberSource,
                sourceMember.Type,
                targetMember.Type,
                configure.MapperReuses,
                compilation,
                out var mappedSourceExpression))
            {
                diagnostics.Add(new DiagnosticInfo(
                    IncompatibleTargetMemberType,
                    targetMember.Location ?? location,
                    targetMember.Name,
                    sourceMember.Type.ToDisplayString(TypeDisplayFormat),
                    targetMember.Type.ToDisplayString(TypeDisplayFormat)));
                continue;
            }

            var sourceExpression = mappedSourceExpression.Expression;
            var guardCondition = CombineConditions(
                CreateIgnoreGuardCondition(configure.IgnoredMappings, targetMember.Name),
                memberNullGuardCondition);
            if (guardCondition is null || !targetMember.CanAssignAfterConstruction)
            {
                assignments.Add(new MemberAssignmentModel(
                    targetMember.Name,
                    guardCondition is null ? sourceExpression : "(" + guardCondition + ") ? " + sourceExpression + " : default",
                    null));
            }
            else
            {
                assignments.Add(new MemberAssignmentModel(targetMember.Name, sourceExpression, guardCondition));
            }

            collectionMappings.AddRange(mappedSourceExpression.CollectionMappings);
        }

        var finalCollectionMappings = DeduplicateCollectionMappings(collectionMappings.ToImmutable());
        return new MappingModel(
            mappingPair.SourceType.ToDisplayString(TypeDisplayFormat),
            mappingPair.TargetType.ToDisplayString(TypeDisplayFormat),
            mappingPair.SourceType.IsValueType,
            inheritsMapperBase,
            hasConfigureMethod,
            UsesServiceProviderMapper(constructor, assignments, finalCollectionMappings),
            constructor,
            [.. assignments.OrderBy(static assignment => assignment.DeclarationOrder)],
            finalCollectionMappings,
            configure.ConfigureLocals);
    }

    private static ConstructorMappingModel? CreateConstructorMapping(
        INamedTypeSymbol mapperSymbol,
        MappingPair mappingPair,
        SourceMemberIndex sourceMembers,
        Dictionary<ITypeSymbol, ImmutableArray<TargetMemberModel>> targetMemberCache,
        ConfigureModel configure,
        Compilation compilation,
        Location location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        if (mappingPair.TargetType is not INamedTypeSymbol targetNamedType)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedTargetConstruction,
                location,
                mapperSymbol.Name,
                mappingPair.TargetType.ToDisplayString(TypeDisplayFormat),
                "target type is not a named type"));
            return null;
        }

        if (targetNamedType.TypeKind is TypeKind.Interface or TypeKind.Delegate ||
            targetNamedType.IsAbstract ||
            targetNamedType.IsStatic)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedTargetConstruction,
                location,
                mapperSymbol.Name,
                mappingPair.TargetType.ToDisplayString(TypeDisplayFormat),
                "target type must be concrete"));
            return null;
        }

        if (targetNamedType.IsValueType)
        {
            return new ConstructorMappingModel(
                ImmutableArray<ConstructorArgumentModel>.Empty,
                ImmutableArray<CollectionMappingModel>.Empty);
        }

        var constructors = targetNamedType.InstanceConstructors
            .Where(static constructor => constructor.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(static constructor => constructor.Parameters.Length)
            .ThenBy(static constructor => constructor.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue);

        foreach (var constructor in constructors)
        {
            if (TryCreateConstructorMapping(constructor, sourceMembers, targetMemberCache, configure, compilation, diagnostics, out var constructorMapping))
            {
                return constructorMapping;
            }
        }

        diagnostics.Add(new DiagnosticInfo(
            UnsupportedTargetConstruction,
            location,
            mapperSymbol.Name,
            mappingPair.TargetType.ToDisplayString(TypeDisplayFormat),
            "no public constructor has parameters that can all be mapped from source members or explicit Map declarations. Add Map/Ignore/default fallback declarations for constructor parameters"));
        return null;
    }

    private static bool TryCreateConstructorMapping(
        IMethodSymbol constructor,
        SourceMemberIndex sourceMembers,
        Dictionary<ITypeSymbol, ImmutableArray<TargetMemberModel>> targetMemberCache,
        ConfigureModel configure,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        out ConstructorMappingModel constructorMapping)
    {
        var arguments = ImmutableArray.CreateBuilder<ConstructorArgumentModel>();
        var collectionMappings = ImmutableArray.CreateBuilder<CollectionMappingModel>();
        var constructorSourceIndex = 0;

        foreach (var parameter in constructor.Parameters)
        {
            var parameterType = parameter.Type.ToDisplayString(TypeDisplayFormat);
            var runtimeParameterType = parameter.Type.TypeKind == TypeKind.Dynamic
                ? "object"
                : parameter.Type.ToDisplayString(ValiantGeneratorTypeDisplayFormats.MetadataTypeDisplayFormat);
            ConstructorArgumentModel CreateArgument(string expression) => new(parameter.Name, parameterType, runtimeParameterType, expression);
            if (TryGetIgnore(configure.IgnoredMappings, parameter.Name, out var ignoredMapping))
            {
                if (ignoredMapping.ConditionExpression is not null)
                {
                    diagnostics.Add(new DiagnosticInfo(
                        UnsupportedMappingDeclaration,
                        ignoredMapping.Location,
                        "Conditional Ignore declarations cannot target constructor parameters."));
                    constructorMapping = EmptyConstructorMapping();
                    return false;
                }

                arguments.Add(CreateArgument("default"));
                continue;
            }

            var explicitMappings = GetExplicitMappings(configure.ExplicitMappings, parameter.Name);
            if (explicitMappings.Length > 0)
            {
                var explicitSourceExpression = "default";
                for (var index = explicitMappings.Length - 1; index >= 0; index--)
                {
                    var explicitMapping = explicitMappings[index];
                    var explicitSource = explicitMapping.SourceExpression;
                    var nullGuardCondition = CaptureNullGuardedSource(
                        ref explicitSource, explicitMapping.SourceType, explicitMapping.NullHandling,
                        "__valiantConstructorSource" + constructorSourceIndex++.ToString(CultureInfo.InvariantCulture));
                    if (!TryCreateMappedExpression(
                        explicitSource,
                        explicitMapping.SourceType,
                        parameter.Type,
                        configure.MapperReuses,
                        compilation,
                        out var mappedExpression))
                    {
                        constructorMapping = EmptyConstructorMapping();
                        return false;
                    }

                    var mappingCondition = CombineConditions(explicitMapping.ConditionExpression, nullGuardCondition);
                    var fallbackExpression = explicitMapping.FallbackExpression ?? explicitSourceExpression;

                    var ruleExpression = mappingCondition is null
                        ? mappedExpression.Expression
                        : "(" + mappingCondition + ") ? " + mappedExpression.Expression + " : " + fallbackExpression;
                    explicitSourceExpression = explicitMapping.BranchConditionExpression is null
                        ? ruleExpression
                        : "(" + explicitMapping.BranchConditionExpression + ") ? (" + ruleExpression + ") : " + explicitSourceExpression;
                    collectionMappings.AddRange(mappedExpression.CollectionMappings);
                }

                arguments.Add(CreateArgument(explicitSourceExpression));
                continue;
            }

            if (configure.DisablesConventionMapping)
            {
                constructorMapping = EmptyConstructorMapping();
                return false;
            }

            if (TryGetSourceMember(sourceMembers, parameter.Name, out var sourceMember) ||
                TryGetFlattenedSourceMember(sourceMembers, parameter.Name, out sourceMember))
            {
                var constructorSource = sourceMember.Expression;
                var nullGuardCondition = CaptureNullGuardedSource(
                    ref constructorSource, sourceMember.Type, NullHandlingPolicy.Default,
                    "__valiantConstructorSource" + constructorSourceIndex++.ToString(CultureInfo.InvariantCulture));
                if (!TryCreateMappedExpression(
                    constructorSource,
                    sourceMember.Type,
                    parameter.Type,
                    configure.MapperReuses,
                    compilation,
                    out var mappedSourceExpression))
                {
                    constructorMapping = EmptyConstructorMapping();
                    return false;
                }

                var sourceExpression = mappedSourceExpression.Expression;
                arguments.Add(CreateArgument(
                    nullGuardCondition is null
                        ? sourceExpression
                        : "(" + nullGuardCondition + ") ? " + sourceExpression + " : default"));
                collectionMappings.AddRange(mappedSourceExpression.CollectionMappings);
                continue;
            }

            if (TryCreateUnflattenedExpression(
                    parameter.Name,
                    parameter.Type,
                    sourceMembers,
                    targetMemberCache,
                    configure.MapperReuses,
                    compilation,
                    out var unflattenedMappedExpression))
            {
                arguments.Add(CreateArgument(unflattenedMappedExpression.Expression));
                collectionMappings.AddRange(unflattenedMappedExpression.CollectionMappings);
                continue;
            }

            constructorMapping = EmptyConstructorMapping();
            return false;
        }

        constructorMapping = new ConstructorMappingModel(
            arguments.ToImmutable(),
            DeduplicateCollectionMappings(collectionMappings.ToImmutable()));
        return true;
    }

    private static ConstructorMappingModel EmptyConstructorMapping()
    {
        return new ConstructorMappingModel(
            ImmutableArray<ConstructorArgumentModel>.Empty,
            ImmutableArray<CollectionMappingModel>.Empty);
    }

    private static SourceMemberIndex GetSourceMembers(ITypeSymbol sourceType)
    {
        var members = ImmutableArray.CreateBuilder<SourceMemberModel>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in EnumerateVisibleMembers(sourceType))
        {
            switch (member)
            {
                case IPropertySymbol { IsStatic: false, IsIndexer: false, GetMethod.DeclaredAccessibility: Accessibility.Public } property:
                    AddSourceMember(property.Name, property.Type, "source." + property.Name, members, seen);
                    break;
                case IFieldSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } field:
                    AddSourceMember(field.Name, field.Type, "source." + field.Name, members, seen);
                    break;
            }
        }

        return new SourceMemberIndex(members.ToImmutable());
    }

    private static ImmutableArray<TargetMemberModel> GetTargetMembers(ITypeSymbol targetType)
    {
        var members = ImmutableArray.CreateBuilder<TargetMemberModel>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in EnumerateVisibleMembers(targetType))
        {
            switch (member)
            {
                case IPropertySymbol { IsStatic: false, IsIndexer: false, SetMethod.DeclaredAccessibility: Accessibility.Public } property:
                    AddTargetMember(property.Name, property.Type, property.IsRequired, !property.SetMethod.IsInitOnly, property.Locations.FirstOrDefault(), members, seen);
                    break;
                case IFieldSymbol { IsStatic: false, IsReadOnly: false, DeclaredAccessibility: Accessibility.Public } field:
                    AddTargetMember(field.Name, field.Type, field.IsRequired, true, field.Locations.FirstOrDefault(), members, seen);
                    break;
            }
        }

        return members.ToImmutable();
    }

    private static ImmutableArray<TargetMemberModel> GetTargetMembers(
        ITypeSymbol targetType,
        Dictionary<ITypeSymbol, ImmutableArray<TargetMemberModel>> cache)
    {
        if (!cache.TryGetValue(targetType, out var members))
        {
            members = GetTargetMembers(targetType);
            cache.Add(targetType, members);
        }

        return members;
    }

    private static IEnumerable<ISymbol> EnumerateVisibleMembers(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            foreach (var member in type.GetMembers())
            {
                yield return member;
            }

            foreach (var interfaceType in type.AllInterfaces)
            {
                foreach (var member in interfaceType.GetMembers())
                {
                    yield return member;
                }
            }

            yield break;
        }

        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                yield return member;
            }
        }
    }

    private static void AddSourceMember(
        string name,
        ITypeSymbol type,
        string expression,
        ImmutableArray<SourceMemberModel>.Builder members,
        HashSet<string> seen)
    {
        if (seen.Add(name))
        {
            members.Add(new SourceMemberModel(name, type, expression));
        }
    }

    private static string CreateNestedSourceExpression(string receiverExpression, ITypeSymbol receiverType, string memberName)
    {
        return CanBeNull(receiverType)
            ? "(" + receiverExpression + " is null ? default : " + receiverExpression + "." + memberName + ")"
            : receiverExpression + "." + memberName;
    }

    private static bool CanFlattenSourceType(ITypeSymbol type)
    {
        var nonNullableType = GetNonNullableType(type);
        if (nonNullableType.TypeKind is TypeKind.Enum or TypeKind.TypeParameter ||
            nonNullableType.SpecialType != SpecialType.None)
        {
            return false;
        }

        if (IsSystemNamespace(nonNullableType) ||
            TryGetEnumerableElementType(nonNullableType, out _))
        {
            return false;
        }

        return nonNullableType.TypeKind is TypeKind.Class or TypeKind.Struct;
    }

    private static void AddTargetMember(
        string name,
        ITypeSymbol type,
        bool isRequired,
        bool canAssignAfterConstruction,
        Location? location,
        ImmutableArray<TargetMemberModel>.Builder members,
        HashSet<string> seen)
    {
        if (seen.Add(name))
        {
            members.Add(new TargetMemberModel(name, type, isRequired, canAssignAfterConstruction, location));
        }
    }

    private static bool TryGetSourceMember(
        SourceMemberIndex sourceMembers,
        string targetName,
        out SourceMemberModel sourceMember)
    {
        return sourceMembers.TryGet(targetName, out sourceMember);
    }

    private static bool TryGetFlattenedSourceMember(
        SourceMemberIndex sourceMembers,
        string targetName,
        out SourceMemberModel sourceMember)
    {
        return TryGetFlattenedSourceMember(
                sourceMembers.Members,
                targetName,
                targetName,
                StringComparison.Ordinal,
                out sourceMember) ||
            TryGetFlattenedSourceMember(
                sourceMembers.Members,
                targetName,
                targetName,
                StringComparison.OrdinalIgnoreCase,
                out sourceMember);
    }

    private static bool TryGetFlattenedSourceMember(
        ImmutableArray<SourceMemberModel> sourceMembers,
        string remainingName,
        string originalTargetName,
        StringComparison comparison,
        out SourceMemberModel sourceMember)
    {
        sourceMember = null!;
        foreach (var member in sourceMembers)
        {
            if (remainingName.Length < member.Name.Length ||
                !remainingName.StartsWith(member.Name, comparison))
            {
                continue;
            }

            var nextRemainingName = remainingName.Substring(member.Name.Length);
            if (nextRemainingName.Length == 0)
            {
                sourceMember = new SourceMemberModel(originalTargetName, member.Type, member.Expression);
                return true;
            }

            if (TryGetFlattenedSourceMember(
                member.Type,
                nextRemainingName,
                originalTargetName,
                member.Expression,
                1,
                comparison,
                out sourceMember))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetFlattenedSourceMember(
        ITypeSymbol currentType,
        string remainingName,
        string originalTargetName,
        string receiverExpression,
        int depth,
        StringComparison comparison,
        out SourceMemberModel sourceMember)
    {
        sourceMember = null!;
        if (depth > 4 || !CanFlattenSourceType(currentType))
        {
            return false;
        }

        foreach (var member in EnumerateVisibleMembers(currentType))
        {
            if (!TryGetReadableSourceMember(member, out var memberName, out var memberType) ||
                remainingName.Length < memberName.Length ||
                !remainingName.StartsWith(memberName, comparison))
            {
                continue;
            }

            var memberExpression = CreateNestedSourceExpression(receiverExpression, currentType, memberName);
            var nextRemainingName = remainingName.Substring(memberName.Length);
            if (nextRemainingName.Length == 0)
            {
                sourceMember = new SourceMemberModel(originalTargetName, memberType, memberExpression);
                return true;
            }

            if (TryGetFlattenedSourceMember(
                memberType,
                nextRemainingName,
                originalTargetName,
                memberExpression,
                depth + 1,
                comparison,
                out sourceMember))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetReadableSourceMember(ISymbol member, out string name, out ITypeSymbol type)
    {
        switch (member)
        {
            case IPropertySymbol { IsStatic: false, IsIndexer: false, GetMethod.DeclaredAccessibility: Accessibility.Public } property:
                name = property.Name;
                type = property.Type;
                return true;
            case IFieldSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } field:
                name = field.Name;
                type = field.Type;
                return true;
            default:
                name = string.Empty;
                type = null!;
                return false;
        }
    }

    private static bool TryCreateUnflattenedExpression(
        string targetPrefix,
        ITypeSymbol targetType,
        SourceMemberIndex sourceMembers,
        Dictionary<ITypeSymbol, ImmutableArray<TargetMemberModel>> targetMemberCache,
        ImmutableArray<MapperReuseModel> mapperReuses,
        Compilation compilation,
        out MappedExpressionModel mappedExpression)
    {
        mappedExpression = EmptyMappedExpression();
        if (!CanConstructUnflattenedTarget(targetType))
        {
            return false;
        }

        var assignments = ImmutableArray.CreateBuilder<string>();
        var collectionMappings = ImmutableArray.CreateBuilder<CollectionMappingModel>();
        foreach (var nestedTargetMember in GetTargetMembers(targetType, targetMemberCache))
        {
            if (!TryGetSourceMember(sourceMembers, targetPrefix + nestedTargetMember.Name, out var sourceMember) ||
                !TryCreateMappedExpression(
                    sourceMember.Expression,
                    sourceMember.Type,
                    nestedTargetMember.Type,
                    mapperReuses,
                    compilation,
                    out var nestedMappedExpression))
            {
                continue;
            }

            assignments.Add(nestedTargetMember.Name + " = " + nestedMappedExpression.Expression);
            collectionMappings.AddRange(nestedMappedExpression.CollectionMappings);
        }

        if (assignments.Count == 0)
        {
            return false;
        }

        mappedExpression = new MappedExpressionModel(
            "new " + GetNonNullableDisplayName(targetType) + " { " + string.Join(", ", assignments) + " }",
            DeduplicateCollectionMappings(collectionMappings.ToImmutable()));
        return true;
    }

    private static bool CanConstructUnflattenedTarget(ITypeSymbol targetType)
    {
        var nonNullableTargetType = GetNonNullableType(targetType);
        if (nonNullableTargetType.TypeKind == TypeKind.Struct)
        {
            return true;
        }

        if (nonNullableTargetType is not INamedTypeSymbol namedType ||
            namedType.TypeKind != TypeKind.Class ||
            namedType.IsAbstract ||
            IsSystemNamespace(namedType))
        {
            return false;
        }

        return namedType.InstanceConstructors.Any(static constructor =>
            constructor.Parameters.Length == 0 &&
            constructor.DeclaredAccessibility == Accessibility.Public);
    }

    private static ImmutableArray<ExplicitMemberMappingModel> GetExplicitMappings(
        ImmutableArray<ExplicitMemberMappingModel> explicitMappings,
        string targetName)
    {
        var matches = ImmutableArray.CreateBuilder<ExplicitMemberMappingModel>();
        foreach (var mapping in explicitMappings)
        {
            if (string.Equals(mapping.TargetMemberName, targetName, StringComparison.Ordinal) ||
                string.Equals(mapping.TargetMemberName, targetName, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(mapping);
            }
        }

        return matches.ToImmutable();
    }

    private static bool TryGetIgnore(
        ImmutableArray<IgnoredMemberMappingModel> ignoredMappings,
        string targetName,
        out IgnoredMemberMappingModel ignoredMapping)
    {
        foreach (var mapping in ignoredMappings)
        {
            if (string.Equals(mapping.TargetMemberName, targetName, StringComparison.Ordinal) ||
                string.Equals(mapping.TargetMemberName, targetName, StringComparison.OrdinalIgnoreCase))
            {
                ignoredMapping = mapping;
                return true;
            }
        }

        ignoredMapping = null!;
        return false;
    }

    private static bool HasUnconditionalIgnore(
        ImmutableArray<IgnoredMemberMappingModel> ignoredMappings,
        string targetName)
    {
        foreach (var mapping in ignoredMappings)
        {
            if ((string.Equals(mapping.TargetMemberName, targetName, StringComparison.Ordinal) ||
                string.Equals(mapping.TargetMemberName, targetName, StringComparison.OrdinalIgnoreCase)) &&
                mapping.ConditionExpression is null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasConditionalIgnore(
        ImmutableArray<IgnoredMemberMappingModel> ignoredMappings,
        string targetName)
    {
        foreach (var mapping in ignoredMappings)
        {
            if ((string.Equals(mapping.TargetMemberName, targetName, StringComparison.Ordinal) ||
                string.Equals(mapping.TargetMemberName, targetName, StringComparison.OrdinalIgnoreCase)) &&
                mapping.ConditionExpression is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ConditionsCoverAllPaths(ImmutableArray<string?> conditions)
    {
        if (conditions.Any(static condition => condition is null))
        {
            return true;
        }

        for (var leftIndex = 0; leftIndex < conditions.Length; leftIndex++)
        {
            var left = conditions[leftIndex];
            if (left is null)
            {
                continue;
            }

            for (var rightIndex = leftIndex + 1; rightIndex < conditions.Length; rightIndex++)
            {
                var right = conditions[rightIndex];
                if (right is not null && AreComplementaryConditions(left, right))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AreComplementaryConditions(string left, string right)
    {
        return string.Equals(NegateCondition(left), right, StringComparison.Ordinal) ||
            string.Equals(left, NegateCondition(right), StringComparison.Ordinal);
    }

    private static void AddUnmappedTargetMemberDiagnostics(
        MappingPair mappingPair,
        ImmutableArray<TargetMemberModel> targetMembers,
        SourceMemberIndex sourceMembers,
        Dictionary<ITypeSymbol, ImmutableArray<TargetMemberModel>> targetMemberCache,
        ConfigureModel configure,
        Compilation compilation,
        Location location,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        foreach (var targetMember in targetMembers)
        {
            if (HasUnconditionalIgnore(configure.IgnoredMappings, targetMember.Name) ||
                GetExplicitMappings(configure.ExplicitMappings, targetMember.Name).Length > 0)
            {
                continue;
            }

            if (!configure.DisablesConventionMapping &&
                (TryGetSourceMember(sourceMembers, targetMember.Name, out var sourceMember) ||
                    TryGetFlattenedSourceMember(sourceMembers, targetMember.Name, out sourceMember)) &&
                TryCreateMappedExpression(
                    sourceMember.Expression,
                    sourceMember.Type,
                    targetMember.Type,
                    configure.MapperReuses,
                    compilation,
                    out _))
            {
                continue;
            }

            if (!configure.DisablesConventionMapping &&
                TryCreateUnflattenedExpression(
                    targetMember.Name,
                    targetMember.Type,
                    sourceMembers,
                    targetMemberCache,
                    configure.MapperReuses,
                    compilation,
                    out _))
            {
                continue;
            }

            AddUnmappedTargetMemberDiagnostic(mappingPair, targetMember, location, diagnostics);
        }
    }

    private static string? CreateIgnoreGuardCondition(
        ImmutableArray<IgnoredMemberMappingModel> ignoredMappings,
        string targetName)
    {
        var conditions = ImmutableArray.CreateBuilder<string>();
        foreach (var mapping in ignoredMappings)
        {
            if ((string.Equals(mapping.TargetMemberName, targetName, StringComparison.Ordinal) ||
                string.Equals(mapping.TargetMemberName, targetName, StringComparison.OrdinalIgnoreCase)) &&
                mapping.ConditionExpression is not null)
            {
                conditions.Add(NegateCondition(mapping.ConditionExpression));
            }
        }

        return conditions.Count == 0 ? null : string.Join(" && ", conditions);
    }

    private static bool TryCreateMappedExpression(
        string sourceExpression,
        ITypeSymbol sourceType,
        ITypeSymbol targetType,
        ImmutableArray<MapperReuseModel> mapperReuses,
        Compilation compilation,
        out MappedExpressionModel mappedExpression)
    {
        return TryCreateMappedExpression(
            sourceExpression,
            sourceType,
            targetType,
            mapperReuses,
            compilation,
            0,
            out mappedExpression);
    }

    private static bool TryCreateMappedExpression(
        string sourceExpression,
        ITypeSymbol sourceType,
        ITypeSymbol targetType,
        ImmutableArray<MapperReuseModel> mapperReuses,
        Compilation compilation,
        int depth,
        out MappedExpressionModel mappedExpression)
    {
        if (compilation.ClassifyConversion(sourceType, targetType).IsImplicit)
        {
            mappedExpression = CreateMappedExpression(sourceExpression);
            return true;
        }

        var applicableMappers = mapperReuses
            .Where(mapper => SymbolEqualityComparer.Default.Equals(mapper.SourceType, sourceType) &&
                SymbolEqualityComparer.Default.Equals(mapper.TargetType, targetType))
            .ToImmutableArray();
        if (!applicableMappers.IsEmpty)
        {
            if (applicableMappers[0].BranchCondition is null)
            {
                mappedExpression = new MappedExpressionModel(
                    applicableMappers[0].MapperExpression + ".Map(" + sourceExpression + ")", [], capturesConfigureLocals: true);
                return true;
            }

            const string nestedSource = "__valiantNestedSource";
            var map = TryCreateMappedExpression(nestedSource, sourceType, targetType, [], compilation, depth, out var fallback)
                ? fallback.Expression
                : CreateServiceProviderMappingExpression(sourceType, targetType, nestedSource);
            for (var index = applicableMappers.Length - 1; index >= 0; index--)
            {
                var mapper = applicableMappers[index];
                var mapperCall = mapper.MapperExpression + "!.Map(__valiantNestedSource)";
                map = mapper.BranchCondition is null ? mapperCall : "(" + mapper.BranchCondition + ") ? " + mapperCall + " : " + map;
            }

            mappedExpression = new MappedExpressionModel("MapWithConfiguredMapper<" + sourceType.ToDisplayString(TypeDisplayFormat) + ", " +
                targetType.ToDisplayString(TypeDisplayFormat) + ">(" + sourceExpression + ", " + nestedSource + " => " + map + ")",
                fallback.CollectionMappings,
                capturesConfigureLocals: true);
            return true;
        }

        if (GetNonNullableType(sourceType).TypeKind == TypeKind.Enum &&
            GetNonNullableType(targetType).TypeKind == TypeKind.Enum)
        {
            mappedExpression = CreateMappedExpression("(" + targetType.ToDisplayString(TypeDisplayFormat) + ")" + sourceExpression);
            return true;
        }

        var elementParameter = "__valiantElement" + depth.ToString(CultureInfo.InvariantCulture);
        if (depth < 3 &&
            TryGetEnumerableElementType(sourceType, out var sourceElementType) &&
            TryGetEnumerableElementType(targetType, out var targetElementType) &&
            TryCreateMappedExpression(
                elementParameter,
                sourceElementType,
                targetElementType,
                mapperReuses,
                compilation,
                depth + 1,
                out var mappedElementExpression))
        {
            var sourceTypeName = sourceType.ToDisplayString(TypeDisplayFormat);
            var sourceElementTypeName = sourceElementType.ToDisplayString(TypeDisplayFormat);
            if (targetType is IArrayTypeSymbol)
            {
                mappedExpression = CreateCollectionMappedExpression(
                    CollectionMappingKind.Array,
                    sourceExpression,
                    sourceTypeName,
                    sourceElementTypeName,
                    GetCollectionSourceKind(sourceType),
                    targetElementType,
                    mappedElementExpression,
                    elementParameter,
                    GetReliableCollectionCountExpression(sourceType));
                return true;
            }

            if (CanAssignListToTarget(targetType, targetElementType, compilation))
            {
                mappedExpression = CreateCollectionMappedExpression(
                    CollectionMappingKind.List,
                    sourceExpression,
                    sourceTypeName,
                    sourceElementTypeName,
                    GetCollectionSourceKind(sourceType),
                    targetElementType,
                    mappedElementExpression,
                    elementParameter,
                    GetCollectionCapacityExpression(sourceType));
                return true;
            }

            if (CanAssignHashSetToTarget(targetType, targetElementType, compilation))
            {
                mappedExpression = CreateCollectionMappedExpression(
                    CollectionMappingKind.HashSet,
                    sourceExpression,
                    sourceTypeName,
                    sourceElementTypeName,
                    GetCollectionSourceKind(sourceType),
                    targetElementType,
                    mappedElementExpression,
                    elementParameter,
                    HasHashSetCapacityConstructor(compilation)
                        ? GetReliableCollectionCountExpression(sourceType)
                        : null);
                return true;
            }
        }

        if (CanUseServiceProviderMapper(sourceType, targetType))
        {
            mappedExpression = CreateMappedExpression(CreateServiceProviderMappingExpression(sourceType, targetType, sourceExpression));
            return true;
        }

        mappedExpression = EmptyMappedExpression();
        return false;
    }

    private static string CreateServiceProviderMappingExpression(ITypeSymbol sourceType, ITypeSymbol targetType, string sourceExpression)
    {
        return ServiceProviderMapperInvocationPrefix + GetNonNullableDisplayName(sourceType) + ", " +
            GetNonNullableDisplayName(targetType) + ">(" + sourceExpression + ")";
    }

    private static string GetMapperInterfaceDisplayName(ITypeSymbol sourceType, ITypeSymbol targetType)
    {
        return "global::StaticNorth.Valiant.Mapping.IMapper<" + sourceType.ToDisplayString(TypeDisplayFormat) +
            ", " + targetType.ToDisplayString(TypeDisplayFormat) + ">";
    }

    private static MappedExpressionModel CreateMappedExpression(string expression)
    {
        return new MappedExpressionModel(expression, ImmutableArray<CollectionMappingModel>.Empty);
    }

    private static MappedExpressionModel EmptyMappedExpression()
    {
        return CreateMappedExpression(string.Empty);
    }

    private static MappedExpressionModel CreateCollectionMappedExpression(
        CollectionMappingKind kind,
        string sourceExpression,
        string sourceType,
        string sourceElementType,
        CollectionSourceKind sourceKind,
        ITypeSymbol targetElementType,
        MappedExpressionModel mappedElementExpression,
        string elementParameter,
        string? capacityExpression)
    {
        var targetElementTypeName = targetElementType.ToDisplayString(TypeDisplayFormat);
        var methodName = CreateCollectionMappingMethodName(
            kind,
            sourceKind,
            sourceType,
            sourceElementType,
            targetElementTypeName,
            mappedElementExpression.Expression,
            capacityExpression);
        var collectionMapping = new CollectionMappingModel(
            methodName,
            kind,
            sourceKind,
            sourceType,
            sourceElementType,
            targetElementTypeName,
            kind == CollectionMappingKind.Array ? CreateArrayAllocationFormat(targetElementType) : null,
            mappedElementExpression.CapturesConfigureLocals
                ? mappedElementExpression.Expression
                : mappedElementExpression.Expression.Replace(elementParameter, "item"),
            capacityExpression,
            mappedElementExpression.CapturesConfigureLocals);
        var collectionMappings = ImmutableArray.CreateBuilder<CollectionMappingModel>();
        collectionMappings.AddRange(mappedElementExpression.CollectionMappings);
        collectionMappings.Add(collectionMapping);
        // Pass element conversions that use a captured mapper into the collection helper.
        var elementMapperArgument = collectionMapping.RequiresElementMapper
            ? ", " + elementParameter + " => " + mappedElementExpression.Expression
            : string.Empty;
        return new MappedExpressionModel(
            methodName + "(" + sourceExpression + elementMapperArgument + ")",
            DeduplicateCollectionMappings(collectionMappings.ToImmutable()),
            collectionMapping.RequiresElementMapper);
    }

    private static string CreateArrayAllocationFormat(ITypeSymbol elementType)
    {
        var dimensions = new StringBuilder("[{0}]");
        while (elementType is IArrayTypeSymbol nestedArray)
        {
            dimensions.Append('[').Append(',', nestedArray.Rank - 1).Append(']');
            elementType = nestedArray.ElementType;
        }

        return elementType.ToDisplayString(TypeDisplayFormat) + dimensions;
    }

    private static CollectionSourceKind GetCollectionSourceKind(ITypeSymbol sourceType)
    {
        return GetNonNullableType(sourceType) is IArrayTypeSymbol
            ? CollectionSourceKind.Array
            : CollectionSourceKind.Other;
    }

    private static string? GetCollectionCapacityExpression(ITypeSymbol sourceType)
    {
        var nonNullableSourceType = GetNonNullableType(sourceType);
        if (nonNullableSourceType is IArrayTypeSymbol)
        {
            return "source.Length";
        }

        foreach (var member in EnumerateVisibleMembers(nonNullableSourceType))
        {
            if (member is IPropertySymbol
                {
                    Name: "Count",
                    IsStatic: false,
                    IsIndexer: false,
                    Type.SpecialType: SpecialType.System_Int32,
                    GetMethod.DeclaredAccessibility: Accessibility.Public
                })
            {
                return "source.Count";
            }
        }

        return null;
    }

    private static string? GetReliableCollectionCountExpression(ITypeSymbol sourceType)
    {
        var nonNullableSourceType = GetNonNullableType(sourceType);
        if (nonNullableSourceType is IArrayTypeSymbol)
        {
            return "source.Length";
        }

        if (nonNullableSourceType is not INamedTypeSymbol namedSourceType)
        {
            return null;
        }

        if (ValiantSymbolMatching.HasMetadataName(
                namedSourceType.OriginalDefinition,
                "System.Collections.Generic",
                "ICollection`1") ||
            ValiantSymbolMatching.HasMetadataName(
                namedSourceType.OriginalDefinition,
                "System.Collections.Generic",
                "IReadOnlyCollection`1"))
        {
            return "source.Count";
        }

        foreach (var interfaceType in namedSourceType.AllInterfaces)
        {
            if (ValiantSymbolMatching.HasMetadataName(
                    interfaceType.OriginalDefinition,
                    "System.Collections.Generic",
                    "ICollection`1") ||
                ValiantSymbolMatching.HasMetadataName(
                    interfaceType.OriginalDefinition,
                    "System.Collections.Generic",
                    "IReadOnlyCollection`1"))
            {
                return "source.Count";
            }
        }

        return null;
    }

    private static bool HasHashSetCapacityConstructor(Compilation compilation)
    {
        var hashSetType = compilation.GetTypeByMetadataName("System.Collections.Generic.HashSet`1");
        return hashSetType is not null &&
            hashSetType.InstanceConstructors.Any(static constructor =>
                constructor.DeclaredAccessibility == Accessibility.Public &&
                constructor.Parameters.Length == 1 &&
                constructor.Parameters[0].Type.SpecialType == SpecialType.System_Int32);
    }

    private static string CreateCollectionMappingMethodName(
        CollectionMappingKind kind,
        CollectionSourceKind sourceKind,
        string sourceType,
        string sourceElementType,
        string targetElementType,
        string mappedElementExpression,
        string? capacityExpression)
    {
        const ulong offsetA = 14695981039346656037UL;
        const ulong offsetB = 1099511628211UL;
        const ulong prime = 1099511628211UL;
        var hashA = offsetA;
        var hashB = offsetB;

        AddHashValue(kind.ToString());
        AddHashValue(sourceKind.ToString());
        AddHashValue(sourceType);
        AddHashValue(sourceElementType);
        AddHashValue(targetElementType);
        AddHashValue(mappedElementExpression);
        AddHashValue(capacityExpression ?? string.Empty);

        return "__ValiantMapCollection_" +
            hashA.ToString("x16", CultureInfo.InvariantCulture) +
            hashB.ToString("x16", CultureInfo.InvariantCulture);

        void AddHashValue(string value)
        {
            foreach (var character in value)
            {
                hashA = (hashA ^ character) * prime;
                hashB = (hashB ^ (character + 31U)) * prime;
            }

            hashA = (hashA ^ 0xffU) * prime;
            hashB = (hashB ^ 0x7fU) * prime;
        }
    }

    private static ImmutableArray<CollectionMappingModel> DeduplicateCollectionMappings(
        ImmutableArray<CollectionMappingModel> collectionMappings)
    {
        if (collectionMappings.Length < 2)
        {
            return collectionMappings;
        }

        var methodNames = new HashSet<string>(StringComparer.Ordinal);
        var deduplicated = ImmutableArray.CreateBuilder<CollectionMappingModel>();
        foreach (var collectionMapping in collectionMappings)
        {
            if (methodNames.Add(collectionMapping.MethodName))
            {
                deduplicated.Add(collectionMapping);
            }
        }

        return deduplicated.ToImmutable();
    }

    private static bool CanUseServiceProviderMapper(ITypeSymbol sourceType, ITypeSymbol targetType)
    {
        var nonNullableSourceType = GetNonNullableType(sourceType);
        var nonNullableTargetType = GetNonNullableType(targetType);
        if (nonNullableSourceType.TypeKind is TypeKind.Enum or TypeKind.TypeParameter ||
            nonNullableTargetType.TypeKind is TypeKind.Enum or TypeKind.TypeParameter ||
            nonNullableSourceType.SpecialType != SpecialType.None ||
            nonNullableTargetType.SpecialType != SpecialType.None ||
            TryGetEnumerableElementType(nonNullableSourceType, out _) ||
            TryGetEnumerableElementType(nonNullableTargetType, out _))
        {
            return false;
        }

        if (IsSystemNamespace(nonNullableSourceType) || IsSystemNamespace(nonNullableTargetType))
        {
            return false;
        }

        return nonNullableSourceType.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface &&
            nonNullableTargetType.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface;
    }

    private static bool UsesServiceProviderMapper(
        ConstructorMappingModel constructor,
        ImmutableArray<MemberAssignmentModel>.Builder assignments,
        ImmutableArray<CollectionMappingModel> collectionMappings)
    {
        return constructor.Arguments.Any(static argument => UsesServiceProviderMapper(argument.SourceExpression)) ||
            assignments.Any(static assignment => UsesServiceProviderMapper(assignment.SourceExpression)) ||
            collectionMappings.Any(static mapping => UsesServiceProviderMapper(mapping.MappedElementExpression));
    }

    private static bool UsesServiceProviderMapper(string expression)
    {
        return expression.Contains(ServiceProviderMapperInvocationPrefix, StringComparison.Ordinal);
    }

    private static string? CaptureNullGuardedSource(
        ref string sourceExpression,
        ITypeSymbol sourceType,
        NullHandlingPolicy nullHandling,
        string localName)
    {
        if (!CanBeNull(sourceType) || nullHandling == NullHandlingPolicy.MapNulls)
        {
            return null;
        }

        var capture = "(" + sourceExpression + ") is var " + localName;
        sourceExpression = localName;
        var nonNull = "(object?)" + localName + " is not null";
        return nullHandling == NullHandlingPolicy.IgnoreSourceNulls
            ? capture + " && " + nonNull
            : capture + " && (GetEffectiveNullHandling() != global::StaticNorth.Valiant.Mapping.ValiantNullHandling.IgnoreSourceNulls || " + nonNull + ")";
    }

    private static bool TryGetEnumerableElementType(ITypeSymbol type, out ITypeSymbol elementType)
    {
        var nonNullableType = GetNonNullableType(type);
        if (nonNullableType.SpecialType == SpecialType.System_String)
        {
            elementType = null!;
            return false;
        }

        if (nonNullableType is IArrayTypeSymbol arrayType)
        {
            elementType = arrayType.ElementType;
            return true;
        }

        if (nonNullableType is INamedTypeSymbol namedType)
        {
            if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T &&
                namedType.TypeArguments.Length == 1)
            {
                elementType = namedType.TypeArguments[0];
                return true;
            }

            foreach (var interfaceType in namedType.AllInterfaces)
            {
                if (interfaceType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T &&
                    interfaceType.TypeArguments.Length == 1)
                {
                    elementType = interfaceType.TypeArguments[0];
                    return true;
                }
            }
        }

        elementType = null!;
        return false;
    }

    private static bool CanAssignListToTarget(ITypeSymbol targetType, ITypeSymbol targetElementType, Compilation compilation)
    {
        var listType = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
        if (listType is null)
        {
            return false;
        }

        var constructedListType = listType.Construct(targetElementType);
        return compilation.ClassifyConversion(constructedListType, targetType).IsImplicit;
    }

    private static bool CanAssignHashSetToTarget(ITypeSymbol targetType, ITypeSymbol targetElementType, Compilation compilation)
    {
        var hashSetType = compilation.GetTypeByMetadataName("System.Collections.Generic.HashSet`1");
        if (hashSetType is null)
        {
            return false;
        }

        var constructedHashSetType = hashSetType.Construct(targetElementType);
        return compilation.ClassifyConversion(constructedHashSetType, targetType).IsImplicit;
    }

    private static ITypeSymbol GetNonNullableType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 } nullableType
            ? nullableType.TypeArguments[0]
            : type;
    }

    private static string GetNonNullableDisplayName(ITypeSymbol type)
    {
        return GetNonNullableType(type) is INamedTypeSymbol namedType
            ? namedType.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(TypeDisplayFormat)
            : type.ToDisplayString(TypeDisplayFormat);
    }

    private static bool CanBeNull(ITypeSymbol type)
    {
        return !type.IsValueType ||
            type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
    }

    private static bool IsSystemNamespace(ITypeSymbol type)
    {
        var namespaceName = type.ContainingNamespace.ToDisplayString();
        return string.Equals(namespaceName, "System", StringComparison.Ordinal) ||
            namespaceName.StartsWith("System.", StringComparison.Ordinal);
    }

    private static void AddUnmappedTargetMemberDiagnostic(
        MappingPair mappingPair,
        TargetMemberModel targetMember,
        Location? fallbackLocation,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var descriptor = targetMember.IsRequired
            ? RequiredTargetMemberNotMapped
            : TargetMemberNotMapped;
        diagnostics.Add(new DiagnosticInfo(
            descriptor,
            fallbackLocation ?? targetMember.Location,
            targetMember.Name,
            mappingPair.SourceType.ToDisplayString(TypeDisplayFormat)));
    }

    private static MapperDeclarationModel CreateMapperDeclarationModel(INamedTypeSymbol mapperSymbol, Location? location)
    {
        var containingTypes = ImmutableArray.CreateBuilder<ContainingTypeModel>();
        var containingTypeStack = new Stack<INamedTypeSymbol>();
        for (var containingType = mapperSymbol.ContainingType; containingType is not null; containingType = containingType.ContainingType)
        {
            containingTypeStack.Push(containingType);
        }

        while (containingTypeStack.Count > 0)
        {
            var containingType = containingTypeStack.Pop();
            containingTypes.Add(new ContainingTypeModel(
                GetTypeModifier(containingType),
                GetTypeDeclarationKeyword(containingType),
                GetTypeDeclarationName(containingType)));
        }

        var namespaceName = mapperSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : mapperSymbol.ContainingNamespace.ToDisplayString();
        var userConstructors = ImmutableArray.CreateBuilder<MapperConstructorModel>();
        var constructorSignatures = ImmutableArray.CreateBuilder<string>();
        foreach (var constructor in mapperSymbol.InstanceConstructors)
        {
            if (constructor.IsImplicitlyDeclared)
            {
                continue;
            }

            var parameters = ImmutableArray.CreateBuilder<MapperConstructorParameterModel>();
            foreach (var parameter in constructor.Parameters)
            {
                parameters.Add(new MapperConstructorParameterModel(
                    parameter.Type.ToDisplayString(TypeDisplayFormat),
                    parameter.Name));
            }

            var constructorParameters = parameters.ToImmutable();
            userConstructors.Add(new MapperConstructorModel(constructorParameters));
            constructorSignatures.Add(CreateConstructorSignature(constructorParameters));
        }

        return new MapperDeclarationModel(
            GetHintName(mapperSymbol),
            mapperSymbol.Name,
            namespaceName,
            GetTypeModifier(mapperSymbol),
            GetTypeDeclarationKeyword(mapperSymbol),
            containingTypes.ToImmutable(),
            location,
            userConstructors.Count > 0,
            userConstructors.ToImmutable(),
            constructorSignatures.ToImmutable());
    }

    private static string CreateConstructorSignature(ImmutableArray<MapperConstructorParameterModel> parameters)
    {
        return string.Join("|", parameters.Select(static parameter => parameter.TypeName));
    }

    private static string GetHintName(INamedTypeSymbol mapperSymbol)
    {
        return ValiantHintNames.Create(
            mapperSymbol,
            "Mapper",
            ValiantHintNameStyle.AllNonAlphanumericCharacters);
    }
}
