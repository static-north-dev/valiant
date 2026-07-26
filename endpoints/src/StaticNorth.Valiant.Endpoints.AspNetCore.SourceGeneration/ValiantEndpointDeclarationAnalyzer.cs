using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.SourceGeneration;

/// <summary>
/// Reports missing endpoint marker attributes for directly declared Valiant endpoint interfaces.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValiantEndpointDeclarationAnalyzer : DiagnosticAnalyzer
{
    private const string EndpointNamespace = "StaticNorth.Valiant.Endpoints.AspNetCore";
    private const string EndpointAttributeNamespace = "StaticNorth.Valiant.Endpoints";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        ValiantEndpointGenerator.AmbiguousEndpointShape,
        ValiantEndpointGenerator.MissingEndpointAttribute,
        ValiantEndpointGenerator.MissingEndpointGroupAttribute
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ClassDeclarationSyntax { BaseList: { } baseList } declaration)
        {
            return;
        }

        var directlyImplementsEndpoint = false;
        var directlyImplementsGroup = false;
        foreach (var baseType in baseList.Types)
        {
            var identifier = GetRightmostIdentifier(baseType.Type);
            if (identifier is not ("IEndpoint" or "IEndpointGroup"))
            {
                continue;
            }

            if (context.SemanticModel.GetTypeInfo(baseType.Type, context.CancellationToken).Type is not INamedTypeSymbol type ||
                type.ContainingNamespace.ToDisplayString() != EndpointNamespace)
            {
                continue;
            }

            directlyImplementsEndpoint |= type.MetadataName == "IEndpoint";
            directlyImplementsGroup |= type.MetadataName == "IEndpointGroup";
        }

        if (!directlyImplementsEndpoint && !directlyImplementsGroup)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol declaredType ||
            HasEndpointMarker(declaredType))
        {
            return;
        }

        var location = declaration.Identifier.GetLocation();
        var typeName = declaredType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (directlyImplementsEndpoint && directlyImplementsGroup)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ValiantEndpointGenerator.AmbiguousEndpointShape,
                location,
                typeName));
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            directlyImplementsEndpoint
                ? ValiantEndpointGenerator.MissingEndpointAttribute
                : ValiantEndpointGenerator.MissingEndpointGroupAttribute,
            location,
            typeName));
    }

    private static string? GetRightmostIdentifier(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => GetRightmostIdentifier(qualified.Right),
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            _ => null
        };
    }

    private static bool HasEndpointMarker(INamedTypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            var attributeType = attribute.AttributeClass;
            if (attributeType?.ContainingNamespace.ToDisplayString() == EndpointAttributeNamespace &&
                attributeType.MetadataName is "ValiantEndpointAttribute" or "ValiantEndpointAttribute`1" or "ValiantEndpointGroupAttribute")
            {
                return true;
            }
        }

        return false;
    }
}
