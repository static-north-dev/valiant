using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StaticNorth.Valiant.SourceGeneration;
using static StaticNorth.Valiant.SourceGeneration.ValiantTypeDeclarations;

namespace StaticNorth.Valiant.Validation.SourceGeneration;

/// <summary>
/// Generates Valiant validation implementations and dependency injection registration code.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class ValiantGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat TypeDisplayFormat = ValiantGeneratorTypeDisplayFormats.TypeDisplayFormat;
    private static readonly SymbolDisplayFormat MetadataTypeDisplayFormat = ValiantGeneratorTypeDisplayFormats.MetadataTypeDisplayFormat;

    private static readonly DiagnosticDescriptor ValidatorMustBePartial = new(
        "VLT001",
        "Validator must be partial",
        "Validator '{0}' must be declared partial so Valiant can generate its implementation",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedValidatorShape = new(
        "VLT002",
        "Unsupported validator shape",
        "Validator '{0}' is not supported: {1}",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ConfigureMethodMissing = new(
        "VLT003",
        "Configure method is missing",
        "Validator '{0}' must declare a public method named '{1}' with one ValidationContext<T> parameter",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedValidationDeclaration = new(
        "VLT004",
        "Unsupported validation declaration",
        "{0}",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ValidatorDefinitionInterfaceMissing = new(
        "VLT005",
        "Validator model type contract is missing",
        "Validator '{0}' must inherit ValidatorDefinition<T> or implement IValidatorDefinition<T> so Valiant can determine the model type",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ValidatorModelTypeMismatch = new(
        "VLT006",
        "Validator model type mismatch",
        "Validator '{0}' declares model type '{1}' in its attribute but implements IValidatorDefinition<{2}>",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ValidationAttributeTypeMismatch = new(
        "VLT007",
        "Validation attribute is not valid for member type",
        "Validation attribute '{0}' cannot be used on member '{1}' of type '{2}'; expected {3}",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor RedundantValidationCheck = new(
        "VLT101",
        "Redundant validation check",
        "Validation check '{0}' is redundant because '{1}' already covers it in this property chain",
        "Valiant",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor DuplicateValidationCheck = new(
        "VLT102",
        "Duplicate validation check",
        "Validation check '{0}' is declared more than once in this property chain",
        "Valiant",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor OverriddenFailureBehavior = new(
        "VLT103",
        "Overridden failure behavior",
        "This property chain sets failure behavior more than once; only the last setting is used",
        "Valiant",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor AttributeValidatorHasNoRules = new(
        "VLT104",
        "Attribute-based validator has no validation attributes",
        "Validator '{0}' uses attribute-based validation, but model type '{1}' has no supported validation attributes",
        "Valiant",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor ValidationAttributesIgnored = new(
        "VLT105",
        "Validation attributes conflict with Configure",
        "Validator '{0}' declares Configure, but model type '{1}' has validation attributes. Use either attribute validation or fluent validation, not both.",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateValidatorModel = new(
        "VLT106",
        "Duplicate validator model",
        "Validator '{0}' validates '{1}', but validator '{2}' already validates the same model type. Only one default validator per model type is allowed.",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGenericValiantTypes = CreateValiantTypeProvider(
            context,
            "StaticNorth.Valiant.Validation.ValiantValidatorAttribute");
        var genericValiantTypes = CreateValiantTypeProvider(
            context,
            "StaticNorth.Valiant.Validation.ValiantValidatorAttribute`1");
        var nonGenericValidators = nonGenericValiantTypes
            .Select(static (model, _) => model.Validator)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .WithComparer(ValidatorModelComparer.Instance)
            .WithTrackingName("Validation.NonGenericValidator");
        var genericValidators = genericValiantTypes
            .Select(static (model, _) => model.Validator)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .WithComparer(ValidatorModelComparer.Instance)
            .WithTrackingName("Validation.GenericValidator");

        context.RegisterSourceOutput(nonGenericValidators, static (context, validator) => ExecuteValidator(context, validator));
        context.RegisterSourceOutput(genericValidators, static (context, validator) => ExecuteValidator(context, validator));

        var validators = nonGenericValidators
            .Collect()
            .Combine(genericValidators.Collect())
            .Select(static (tuple, _) => tuple.Left.AddRange(tuple.Right));

        context.RegisterSourceOutput(validators, static (context, validators) => ExecuteDuplicateValidatorModelDiagnostics(context, validators));

        var dependencyInjectionValidators = nonGenericValidators
            .Where(static validator => IsValidValidator(validator))
            .Select(static (validator, _) => CreateDependencyInjectionValidatorModel(validator))
            .Collect()
            .Combine(
                genericValidators
                    .Where(static validator => IsValidValidator(validator))
                    .Select(static (validator, _) => CreateDependencyInjectionValidatorModel(validator))
                    .Collect())
            .Select(static (tuple, _) => tuple.Left.AddRange(tuple.Right));

        var dependencyInjectionPropertyValidators = nonGenericValiantTypes
            .Select(static (model, _) => model.PropertyValidator)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .Collect()
            .Combine(
                genericValiantTypes
                    .Select(static (model, _) => model.PropertyValidator)
                    .Where(static model => model is not null)
                    .Select(static (model, _) => model!.Value)
                    .Collect())
            .Select(static (tuple, _) => tuple.Left.AddRange(tuple.Right));

        var hasDependencyInjection = context.CompilationProvider
            .Select(static (compilation, _) => ValiantDependencyInjectionSymbols.HasDependencyInjection(compilation));

        context.RegisterSourceOutput(
            hasDependencyInjection
                .Combine(dependencyInjectionValidators.Combine(dependencyInjectionPropertyValidators))
                .WithTrackingName("Validation.DependencyInjection"),
            static (context, tuple) => ExecuteDependencyInjection(context, tuple.Left, tuple.Right.Left, tuple.Right.Right));
    }

    private static IncrementalValuesProvider<ValiantTypeModel> CreateValiantTypeProvider(
        IncrementalGeneratorInitializationContext context,
        string attributeMetadataName)
    {
        return context.SyntaxProvider
            .ForAttributeWithMetadataName(
                attributeMetadataName,
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (context, cancellationToken) => CreateValiantTypeModel(context, cancellationToken))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);
    }

    private static bool IsValidValidator(ValidatorModel validator)
    {
        return validator.ModelType is not null &&
            !validator.Diagnostics.Any(static diagnostic => diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error);
    }

    private static bool IsValiantValidatorAttribute(AttributeData attribute)
    {
        var attributeClass = attribute.AttributeClass;
        if (attributeClass is null)
        {
            return false;
        }

        return attributeClass.ContainingNamespace.ToDisplayString() == "StaticNorth.Valiant.Validation" &&
            attributeClass.MetadataName is "ValiantValidatorAttribute" or "ValiantValidatorAttribute`1";
    }

    private static ITypeSymbol? GetAttributeModelType(AttributeData attribute)
    {
        var attributeClass = attribute.AttributeClass;
        if (attributeClass is { MetadataName: "ValiantValidatorAttribute`1", TypeArguments.Length: 1 })
        {
            return attributeClass.TypeArguments[0];
        }

        if (attribute.ConstructorArguments.Length == 1)
        {
            return attribute.ConstructorArguments[0].Value as ITypeSymbol;
        }

        return null;
    }

    private static bool GetRegisterAsDefault(AttributeData attribute)
    {
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (string.Equals(namedArgument.Key, "RegisterAsDefault", StringComparison.Ordinal) &&
                namedArgument.Value.Value is bool registerAsDefault)
            {
                return registerAsDefault;
            }
        }

        return true;
    }

    private static ITypeSymbol? GetValidatorDefinitionModelType(INamedTypeSymbol validatorSymbol)
    {
        foreach (var interfaceType in validatorSymbol.AllInterfaces)
        {
            if (interfaceType.ContainingNamespace.ToDisplayString() == "StaticNorth.Valiant.Validation" &&
                interfaceType is { MetadataName: "IValidatorDefinition`1", TypeArguments.Length: 1 })
            {
                return interfaceType.TypeArguments[0];
            }
        }

        return null;
    }

    private static ITypeSymbol? GetValidatorBaseModelType(INamedTypeSymbol validatorSymbol)
    {
        for (var baseType = validatorSymbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.ContainingNamespace.ToDisplayString() == "StaticNorth.Valiant.Validation" &&
                baseType is { MetadataName: "ValidatorDefinition`1", TypeArguments.Length: 1 })
            {
                return baseType.TypeArguments[0];
            }
        }

        return null;
    }

    private static ValiantTypeModel? CreateValiantTypeModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var validatorSymbol = (INamedTypeSymbol)context.TargetSymbol;
        var attribute = context.Attributes.FirstOrDefault(IsValiantValidatorAttribute);
        if (attribute is null)
        {
            return null;
        }

        var definitionModelType = GetValidatorDefinitionModelType(validatorSymbol);
        var baseModelType = GetValidatorBaseModelType(validatorSymbol);
        var attributeModelType = GetAttributeModelType(attribute);
        var propertyValidator = CreateDependencyInjectionPropertyValidatorModel(validatorSymbol);
        if (propertyValidator is not null &&
            definitionModelType is null &&
            baseModelType is null &&
            attributeModelType is null)
        {
            return new ValiantTypeModel(null, propertyValidator);
        }

        return new ValiantTypeModel(CreateValidatorModel(context, cancellationToken), null);
    }

    private static DependencyInjectionPropertyValidatorModel? CreateDependencyInjectionPropertyValidatorModel(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.IsAbstract ||
            typeSymbol.IsStatic ||
            typeSymbol.IsGenericType)
        {
            return null;
        }

        for (var containingType = typeSymbol.ContainingType; containingType is not null; containingType = containingType.ContainingType)
        {
            if (containingType.IsGenericType)
            {
                return null;
            }
        }

        var propertyTypes = ImmutableArray.CreateBuilder<string>();
        var propertyTypeSet = new HashSet<string>(StringComparer.Ordinal);
        if (TryGetPropertyValidatorDefinitionArgument(typeSymbol, out var propertyType))
        {
            AddPropertyValidatorType(propertyTypes, propertyTypeSet, propertyType);
        }

        if (propertyTypes.Count == 0)
        {
            return null;
        }

        return new DependencyInjectionPropertyValidatorModel(
            typeSymbol.ToDisplayString(TypeDisplayFormat),
            propertyTypes.ToImmutable());
    }

    private static void AddPropertyValidatorType(
        ImmutableArray<string>.Builder propertyTypes,
        HashSet<string> propertyTypeSet,
        ITypeSymbol propertyType)
    {
        var displayName = propertyType.ToDisplayString(TypeDisplayFormat);
        if (propertyTypeSet.Add(displayName))
        {
            propertyTypes.Add(displayName);
        }
    }

    private static ValidatorModel? CreateValidatorModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var classSyntax = (ClassDeclarationSyntax)context.TargetNode;
        var validatorSymbol = (INamedTypeSymbol)context.TargetSymbol;
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        if (!classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            diagnostics.Add(new DiagnosticInfo(
                ValidatorMustBePartial,
                classSyntax.Identifier.GetLocation(),
                validatorSymbol.Name));
        }

        for (var containingType = validatorSymbol.ContainingType; containingType is not null; containingType = containingType.ContainingType)
        {
            if (containingType.IsGenericType)
            {
                diagnostics.Add(new DiagnosticInfo(
                    UnsupportedValidatorShape,
                    classSyntax.Identifier.GetLocation(),
                    validatorSymbol.Name,
                    "nested validators inside generic containing types are not supported yet"));
                break;
            }
        }

        for (SyntaxNode? parent = classSyntax.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is TypeDeclarationSyntax containingTypeSyntax &&
                !containingTypeSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                diagnostics.Add(new DiagnosticInfo(
                    UnsupportedValidatorShape,
                    containingTypeSyntax.Identifier.GetLocation(),
                    validatorSymbol.Name,
                    "containing types for nested validators must be partial"));
            }
        }

        if (validatorSymbol.IsGenericType)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedValidatorShape,
                classSyntax.Identifier.GetLocation(),
                validatorSymbol.Name,
                "generic validator types are not supported"));
        }

        if (validatorSymbol.IsAbstract || validatorSymbol.IsStatic)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedValidatorShape,
                classSyntax.Identifier.GetLocation(),
                validatorSymbol.Name,
                "validators must be concrete instance types"));
        }

        var attribute = context.Attributes.FirstOrDefault(IsValiantValidatorAttribute);

        if (attribute is null)
        {
            return null;
        }

        var declaration = CreateValidatorDeclarationModel(validatorSymbol, classSyntax.Identifier.GetLocation());
        var registerAsDefault = GetRegisterAsDefault(attribute);
        var attributeModelType = GetAttributeModelType(attribute);
        var definitionModelType = GetValidatorDefinitionModelType(validatorSymbol);
        var baseModelType = GetValidatorBaseModelType(validatorSymbol);
        var implementsValidatorDefinition = definitionModelType is not null;
        var inheritsValidatorBase = baseModelType is not null;
        var modelType = definitionModelType ?? baseModelType;
        if (modelType is null)
        {
            diagnostics.Add(new DiagnosticInfo(
                ValidatorDefinitionInterfaceMissing,
                classSyntax.Identifier.GetLocation(),
                validatorSymbol.Name));

            var usings = CollectUsings(classSyntax);
            var emptyNodes = ImmutableArray<ValidationNode>.Empty;
            var diagnosticsArray = diagnostics.ToImmutable();
            return new ValidatorModel(
                declaration,
                null,
                null,
                false,
                false,
                false,
                false,
                registerAsDefault,
                GetConfigureMethodName(attribute),
                usings,
                emptyNodes,
                diagnosticsArray,
                CreateValidatorFingerprint(declaration, null, null, false, false, false, false, registerAsDefault, GetConfigureMethodName(attribute), usings, emptyNodes, diagnosticsArray));
        }

        if (definitionModelType is not null &&
            baseModelType is not null &&
            !SymbolEqualityComparer.Default.Equals(definitionModelType, baseModelType))
        {
            diagnostics.Add(new DiagnosticInfo(
                ValidatorModelTypeMismatch,
                classSyntax.Identifier.GetLocation(),
                validatorSymbol.Name,
                baseModelType.ToDisplayString(TypeDisplayFormat),
                definitionModelType.ToDisplayString(TypeDisplayFormat)));
        }

        if (attributeModelType is not null && !SymbolEqualityComparer.Default.Equals(attributeModelType, modelType))
        {
            diagnostics.Add(new DiagnosticInfo(
                ValidatorModelTypeMismatch,
                classSyntax.Identifier.GetLocation(),
                validatorSymbol.Name,
                attributeModelType.ToDisplayString(TypeDisplayFormat),
                modelType.ToDisplayString(TypeDisplayFormat)));
        }

        var configureMethodName = GetConfigureMethodName(attribute);
        var methodSyntax = FindConfigureMethod(validatorSymbol, configureMethodName, cancellationToken);
        if (methodSyntax is null)
        {
            var usings = CollectUsings(classSyntax);
            var attributeNodeBuilder = ImmutableArray.CreateBuilder<ValidationNode>();
            if (inheritsValidatorBase)
            {
                AddAttributeValidationNodes(modelType, attributeNodeBuilder, diagnostics);
                AnalyzeValidationSuggestions(attributeNodeBuilder, diagnostics);
                if (attributeNodeBuilder.Count == 0)
                {
                    diagnostics.Add(new DiagnosticInfo(
                        AttributeValidatorHasNoRules,
                        classSyntax.Identifier.GetLocation(),
                        validatorSymbol.Name,
                        modelType.ToDisplayString(TypeDisplayFormat)));
                }
            }
            else
            {
                diagnostics.Add(new DiagnosticInfo(
                    ConfigureMethodMissing,
                    classSyntax.Identifier.GetLocation(),
                    validatorSymbol.Name,
                    configureMethodName));
            }

            var attributeNodes = attributeNodeBuilder.ToImmutable();
            var diagnosticsArray = diagnostics.ToImmutable();
            var modelTypeName = modelType.ToDisplayString(TypeDisplayFormat);
            var metadataModelTypeName = modelType.ToDisplayString(MetadataTypeDisplayFormat);
            return new ValidatorModel(
                declaration,
                modelTypeName,
                metadataModelTypeName,
                modelType.IsValueType,
                inheritsValidatorBase,
                implementsValidatorDefinition,
                false,
                registerAsDefault,
                configureMethodName,
                usings,
                attributeNodes,
                diagnosticsArray,
                CreateValidatorFingerprint(declaration, modelTypeName, metadataModelTypeName, modelType.IsValueType, inheritsValidatorBase, implementsValidatorDefinition, false, registerAsDefault, configureMethodName, usings, attributeNodes, diagnosticsArray));
        }

        if (inheritsValidatorBase && HasSupportedValidationAttributes(modelType))
        {
            diagnostics.Add(new DiagnosticInfo(
                ValidationAttributesIgnored,
                classSyntax.Identifier.GetLocation(),
                validatorSymbol.Name,
                modelType.ToDisplayString(TypeDisplayFormat)));
        }

        if (!methodSyntax.Modifiers.Any(SyntaxKind.PublicKeyword))
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                methodSyntax.Identifier.GetLocation(),
                $"Configure method '{configureMethodName}' must be public."));
        }

        if (methodSyntax.ParameterList.Parameters.Count != 1)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedValidationDeclaration,
                methodSyntax.Identifier.GetLocation(),
                $"Configure method '{configureMethodName}' must have exactly one parameter."));
        }

        var nodes = ImmutableArray.CreateBuilder<ValidationNode>();
        var generateSimpleFluent = false;
        if (methodSyntax.ParameterList.Parameters.Count == 1)
        {
            var semanticModel = context.SemanticModel.Compilation.GetSemanticModel(methodSyntax.SyntaxTree);
            var parameterName = methodSyntax.ParameterList.Parameters[0].Identifier.ValueText;

            if (methodSyntax.Body is not null)
            {
                ParseStatements(methodSyntax.Body.Statements, ParseScope.Root(parameterName), semanticModel, cancellationToken, nodes, diagnostics);
                AnalyzeValidationSuggestions(nodes, diagnostics);
                generateSimpleFluent = CanGenerateSimpleFluentValidator(validatorSymbol, modelType, methodSyntax, semanticModel, nodes, cancellationToken);
            }
            else if (methodSyntax.ExpressionBody?.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.ValueText == parameterName)
            {
                // Expression-bodied empty declarations are allowed for compatibility: static ValidationContext<T> Configure(...) => context;
            }
            else
            {
                diagnostics.Add(new DiagnosticInfo(
                    UnsupportedValidationDeclaration,
                    methodSyntax.Identifier.GetLocation(),
                    $"Configure method '{configureMethodName}' must contain supported validation declarations."));
            }
        }

        var finalUsings = CollectUsings(classSyntax);
        var finalNodes = nodes.ToImmutable();
        var finalDiagnostics = diagnostics.ToImmutable();
        var finalModelTypeName = modelType.ToDisplayString(TypeDisplayFormat);
        var finalMetadataModelTypeName = modelType.ToDisplayString(MetadataTypeDisplayFormat);
        return new ValidatorModel(
            declaration,
            finalModelTypeName,
            finalMetadataModelTypeName,
            modelType.IsValueType,
            inheritsValidatorBase,
            implementsValidatorDefinition,
            true,
            registerAsDefault,
            configureMethodName,
            finalUsings,
            finalNodes,
            finalDiagnostics,
            CreateValidatorFingerprint(declaration, finalModelTypeName, finalMetadataModelTypeName, modelType.IsValueType, inheritsValidatorBase, implementsValidatorDefinition, true, registerAsDefault, configureMethodName, finalUsings, finalNodes, finalDiagnostics, generateSimpleFluent),
            generateSimpleFluent);
    }

    private static ValidatorDeclarationModel CreateValidatorDeclarationModel(INamedTypeSymbol validatorSymbol, Location? location)
    {
        var containingTypes = ImmutableArray.CreateBuilder<ContainingTypeModel>();
        var containingTypeStack = new Stack<INamedTypeSymbol>();
        for (var containingType = validatorSymbol.ContainingType; containingType is not null; containingType = containingType.ContainingType)
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

        var constructor = SelectConstructorToChain(validatorSymbol);
        ConstructorModel? constructorModel = null;
        if (constructor is not null)
        {
            var parameters = ImmutableArray.CreateBuilder<ConstructorParameterModel>();
            foreach (var parameter in constructor.Parameters)
            {
                parameters.Add(new ConstructorParameterModel(
                    parameter.Type.ToDisplayString(TypeDisplayFormat),
                    parameter.Name));
            }

            constructorModel = new ConstructorModel(parameters.ToImmutable());
        }

        var namespaceName = validatorSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : validatorSymbol.ContainingNamespace.ToDisplayString();
        var validatorType = validatorSymbol.ToDisplayString(TypeDisplayFormat);
        var metadataProviderTypeName = GetMetadataProviderTypeName(validatorType);
        var metadataProviderType = namespaceName is null
            ? "global::" + metadataProviderTypeName
            : "global::" + namespaceName + "." + metadataProviderTypeName;

        return new ValidatorDeclarationModel(
            GetHintName(validatorSymbol, "Validator"),
            validatorSymbol.Name,
            validatorType,
            namespaceName,
            GetTypeModifier(validatorSymbol),
            metadataProviderType,
            containingTypes.ToImmutable(),
            location,
            constructorModel);
    }
}
