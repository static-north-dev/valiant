using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Valiant.SourceGeneration;

[Generator(LanguageNames.CSharp)]
public sealed partial class ValiantGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat TypeDisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

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
        "Validator definition interface is missing",
        "Validator '{0}' must implement IValidatorDefinition<T> so Valiant can determine the model type and enforce the Configure method contract",
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

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGenericValidators = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Valiant.ValiantValidatorAttribute",
                static (node, _) => node is ClassDeclarationSyntax,
                static (context, cancellationToken) => CreateValidatorModel(context, cancellationToken))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        var genericValidators = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Valiant.ValiantValidatorAttribute`1",
                static (node, _) => node is ClassDeclarationSyntax,
                static (context, cancellationToken) => CreateValidatorModel(context, cancellationToken))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        var validators = nonGenericValidators.Collect()
            .Combine(genericValidators.Collect())
            .Select(static (tuple, _) => tuple.Left.AddRange(tuple.Right));

        var source = context.CompilationProvider.Combine(validators);
        context.RegisterSourceOutput(source, static (context, tuple) => Execute(context, tuple.Left, tuple.Right));
    }

    private static bool IsValiantValidatorAttribute(AttributeData attribute)
    {
        var attributeClass = attribute.AttributeClass;
        if (attributeClass is null)
        {
            return false;
        }

        return attributeClass.ContainingNamespace.ToDisplayString() == "Valiant" &&
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

    private static ITypeSymbol? GetValidatorDefinitionModelType(INamedTypeSymbol validatorSymbol)
    {
        foreach (var interfaceType in validatorSymbol.AllInterfaces)
        {
            if (interfaceType.ContainingNamespace.ToDisplayString() == "Valiant" &&
                interfaceType.MetadataName == "IValidatorDefinition`1" &&
                interfaceType.TypeArguments.Length == 1)
            {
                return interfaceType.TypeArguments[0];
            }
        }

        return null;
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

        if (validatorSymbol.ContainingType is not null)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedValidatorShape,
                classSyntax.Identifier.GetLocation(),
                validatorSymbol.Name,
                "nested validators are not supported yet"));
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

        var attributeModelType = GetAttributeModelType(attribute);
        var modelType = GetValidatorDefinitionModelType(validatorSymbol);
        if (modelType is null)
        {
            diagnostics.Add(new DiagnosticInfo(
                ValidatorDefinitionInterfaceMissing,
                classSyntax.Identifier.GetLocation(),
                validatorSymbol.Name));

            return new ValidatorModel(
                validatorSymbol,
                null,
                GetConfigureMethodName(attribute),
                ImmutableArray<ValidationNode>.Empty,
                diagnostics.ToImmutable());
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
            diagnostics.Add(new DiagnosticInfo(
                ConfigureMethodMissing,
                classSyntax.Identifier.GetLocation(),
                validatorSymbol.Name,
                configureMethodName));

            return new ValidatorModel(
                validatorSymbol,
                modelType,
                configureMethodName,
                ImmutableArray<ValidationNode>.Empty,
                diagnostics.ToImmutable());
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
        if (methodSyntax.ParameterList.Parameters.Count == 1)
        {
            var semanticModel = context.SemanticModel.Compilation.GetSemanticModel(methodSyntax.SyntaxTree);
            var parameterName = methodSyntax.ParameterList.Parameters[0].Identifier.ValueText;

            if (methodSyntax.Body is not null)
            {
                ParseStatements(methodSyntax.Body.Statements, ParseScope.Root(parameterName), semanticModel, cancellationToken, nodes, diagnostics);
                AnalyzeValidationSuggestions(nodes, diagnostics);
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

        return new ValidatorModel(
            validatorSymbol,
            modelType,
            configureMethodName,
            nodes.ToImmutable(),
            diagnostics.ToImmutable());
    }
}
