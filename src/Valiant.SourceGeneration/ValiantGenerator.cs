using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Valiant.SourceGeneration;

[Generator(LanguageNames.CSharp)]
public sealed class ValiantGenerator : IIncrementalGenerator
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
        "Rule declaration method is missing",
        "Validator '{0}' must declare a static method named '{1}' with one ValidationContext<T> parameter that returns ValidationContext<T>",
        "Valiant",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedRuleSyntax = new(
        "VLT004",
        "Unsupported rule syntax",
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
                GetRulesMethodName(attribute),
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

        var rulesMethodName = GetRulesMethodName(attribute);
        var methodSyntax = FindRulesMethod(validatorSymbol, rulesMethodName, cancellationToken);
        if (methodSyntax is null)
        {
            diagnostics.Add(new DiagnosticInfo(
                ConfigureMethodMissing,
                classSyntax.Identifier.GetLocation(),
                validatorSymbol.Name,
                rulesMethodName));

            return new ValidatorModel(
                validatorSymbol,
                modelType,
                rulesMethodName,
                ImmutableArray<ValidationNode>.Empty,
                diagnostics.ToImmutable());
        }

        if (!methodSyntax.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedRuleSyntax,
                methodSyntax.Identifier.GetLocation(),
                $"Rule declaration method '{rulesMethodName}' must be static."));
        }

        if (!methodSyntax.Modifiers.Any(SyntaxKind.PublicKeyword))
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedRuleSyntax,
                methodSyntax.Identifier.GetLocation(),
                $"Rule declaration method '{rulesMethodName}' must be public so it satisfies IValidatorDefinition<T>."));
        }

        if (methodSyntax.ParameterList.Parameters.Count != 1)
        {
            diagnostics.Add(new DiagnosticInfo(
                UnsupportedRuleSyntax,
                methodSyntax.Identifier.GetLocation(),
                $"Rule declaration method '{rulesMethodName}' must have exactly one parameter."));
        }

        var nodes = ImmutableArray.CreateBuilder<ValidationNode>();
        if (methodSyntax.ParameterList.Parameters.Count == 1)
        {
            var semanticModel = context.SemanticModel.Compilation.GetSemanticModel(methodSyntax.SyntaxTree);
            var parameterName = methodSyntax.ParameterList.Parameters[0].Identifier.ValueText;

            if (methodSyntax.Body is not null)
            {
                ParseStatements(methodSyntax.Body.Statements, parameterName, semanticModel, cancellationToken, nodes, diagnostics);
            }
            else if (methodSyntax.ExpressionBody?.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.ValueText == parameterName)
            {
                // Expression-bodied empty declarations are allowed: static ValidationContext<T> Configure(...) => context;
            }
            else
            {
                diagnostics.Add(new DiagnosticInfo(
                    UnsupportedRuleSyntax,
                    methodSyntax.Identifier.GetLocation(),
                    $"Rule declaration method '{rulesMethodName}' must contain RuleFor declarations and return the validation context."));
            }
        }

        return new ValidatorModel(
            validatorSymbol,
            modelType,
            rulesMethodName,
            nodes.ToImmutable(),
            diagnostics.ToImmutable());
    }

    private static void ParseStatements(
        SyntaxList<StatementSyntax> statements,
        string contextParameterName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        foreach (var statement in statements)
        {
            ParseStatement(statement, contextParameterName, semanticModel, cancellationToken, nodes, diagnostics);
        }
    }

    private static void ParseStatement(
        StatementSyntax statement,
        string contextParameterName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (statement)
        {
            case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }:
                if (TryParseRule(invocation, contextParameterName, semanticModel, cancellationToken, out var rule, out var diagnostic))
                {
                    nodes.Add(new RuleNode(rule));
                }
                else if (diagnostic is not null)
                {
                    diagnostics.Add(diagnostic);
                }

                return;

            case IfStatementSyntax ifStatement:
                ParseIfStatement(ifStatement, contextParameterName, semanticModel, cancellationToken, nodes, diagnostics);
                return;

            case SwitchStatementSyntax switchStatement:
                ParseSwitchStatement(switchStatement, contextParameterName, semanticModel, cancellationToken, nodes, diagnostics);
                return;

            case BlockSyntax block:
                ParseStatements(block.Statements, contextParameterName, semanticModel, cancellationToken, nodes, diagnostics);
                return;

            case ReturnStatementSyntax returnStatement when returnStatement.Expression is not null &&
                IsIdentifier(returnStatement.Expression, contextParameterName):
                return;

            case BreakStatementSyntax:
                return;

            default:
                diagnostics.Add(new DiagnosticInfo(
                    UnsupportedRuleSyntax,
                    statement.GetLocation(),
                    "Only RuleFor declarations, if/else statements, switch statements, blocks, break statements, and 'return context;' are supported in validator declarations."));
                return;
        }
    }

    private static void ParseIfStatement(
        IfStatementSyntax ifStatement,
        string contextParameterName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var thenNodes = ParseNestedStatement(ifStatement.Statement, contextParameterName, semanticModel, cancellationToken, diagnostics);
        var elseNodes = ifStatement.Else is null
            ? ImmutableArray<ValidationNode>.Empty
            : ParseNestedStatement(ifStatement.Else.Statement, contextParameterName, semanticModel, cancellationToken, diagnostics);

        nodes.Add(new IfNode(
            RewriteForValidation(ifStatement.Condition, contextParameterName),
            thenNodes,
            elseNodes));
    }

    private static void ParseSwitchStatement(
        SwitchStatementSyntax switchStatement,
        string contextParameterName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<ValidationNode>.Builder nodes,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var sections = ImmutableArray.CreateBuilder<SwitchSectionModel>();
        foreach (var section in switchStatement.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var labels = ImmutableArray.CreateBuilder<string>();
            foreach (var label in section.Labels)
            {
                labels.Add(RewriteForValidation(label, contextParameterName));
            }

            var sectionNodes = ImmutableArray.CreateBuilder<ValidationNode>();
            ParseStatements(section.Statements, contextParameterName, semanticModel, cancellationToken, sectionNodes, diagnostics);
            sections.Add(new SwitchSectionModel(labels.ToImmutable(), sectionNodes.ToImmutable()));
        }

        nodes.Add(new SwitchNode(
            RewriteForValidation(switchStatement.Expression, contextParameterName),
            sections.ToImmutable()));
    }

    private static ImmutableArray<ValidationNode> ParseNestedStatement(
        StatementSyntax statement,
        string contextParameterName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var nodes = ImmutableArray.CreateBuilder<ValidationNode>();
        ParseStatement(statement, contextParameterName, semanticModel, cancellationToken, nodes, diagnostics);
        return nodes.ToImmutable();
    }

    private static void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<ValidatorModel> validators)
    {
        if (validators.IsDefaultOrEmpty)
        {
            return;
        }

        var validValidators = ImmutableArray.CreateBuilder<ValidatorModel>();
        foreach (var validator in validators)
        {
            foreach (var diagnostic in validator.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic.Create());
            }

            if (validator.Diagnostics.Any(static diagnostic => diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error) ||
                validator.ModelType is null)
            {
                continue;
            }

            validValidators.Add(validator);
            context.AddSource(GetHintName(validator.ValidatorSymbol, "Validator"), SourceText.From(GenerateValidator(validator), Encoding.UTF8));
        }

        if (validValidators.Count > 0 && HasDependencyInjection(compilation))
        {
            context.AddSource("Valiant.DependencyInjection.g.cs", SourceText.From(GenerateDependencyInjection(validValidators.ToImmutable()), Encoding.UTF8));
        }
    }

    private static bool HasDependencyInjection(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection") is not null &&
            compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.ServiceDescriptor") is not null &&
            compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions") is not null;
    }

    private static string GenerateValidator(ValidatorModel validator)
    {
        var builder = new StringBuilder();
        var namespaceName = validator.ValidatorSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : validator.ValidatorSymbol.ContainingNamespace.ToDisplayString();
        var modelType = validator.ModelType!.ToDisplayString(TypeDisplayFormat);
        var modifier = GetTypeModifier(validator.ValidatorSymbol);
        var hasAsyncChecks = HasAsyncChecks(validator.Nodes);
        var totalChecks = Math.Max(CountChecks(validator.Nodes), 1);

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");

        if (namespaceName is not null)
        {
            builder.Append("namespace ").Append(namespaceName).AppendLine();
            builder.AppendLine("{");
        }

        AppendIndent(builder, 0, namespaceName);
        builder.Append(modifier).Append(" partial class ").Append(validator.ValidatorSymbol.Name)
            .Append(" : global::Valiant.IValidator<").Append(modelType).AppendLine(">");
        AppendIndent(builder, 0, namespaceName);
        builder.AppendLine("{");

        AppendIndent(builder, 1, namespaceName);
        builder.Append("public global::Valiant.ValidationResult Validate(").Append(modelType).AppendLine(" instance)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");

        if (hasAsyncChecks)
        {
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("throw new global::System.InvalidOperationException(\"This validator contains asynchronous rules. Call ValidateAsync instead.\");");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("}");

            AppendCommonMembers(builder, namespaceName, modelType, validator, totalChecks, hasAsyncChecks);
            AppendIndent(builder, 0, namespaceName);
            builder.AppendLine("}");

            if (namespaceName is not null)
            {
                builder.AppendLine("}");
            }

            return builder.ToString();
        }

        if (!validator.ModelType.IsValueType)
        {
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("if (instance is null)");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, 3, namespaceName);
            builder.AppendLine("throw new global::System.ArgumentNullException(nameof(instance));");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("}");
            builder.AppendLine();
        }

        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("global::System.Collections.Generic.List<global::Valiant.ValidationFailure>? failures = null;");

        var ruleIndex = 0;
        AppendValidationNodes(builder, namespaceName, 2, validator.Nodes, ref ruleIndex, totalChecks, false);

        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return failures is null ? global::Valiant.ValidationResult.Valid : new global::Valiant.ValidationResult(failures);");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
        AppendCommonMembers(builder, namespaceName, modelType, validator, totalChecks, hasAsyncChecks);
        AppendIndent(builder, 0, namespaceName);
        builder.AppendLine("}");

        if (namespaceName is not null)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static void AppendCommonMembers(StringBuilder builder, string? namespaceName, string modelType, ValidatorModel validator, int totalChecks, bool hasAsyncChecks)
    {
        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.Append("public bool IsValid(").Append(modelType).AppendLine(" instance)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return Validate(instance).IsValid;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        builder.AppendLine();
        if (hasAsyncChecks)
        {
            AppendAsyncValidationMembers(builder, namespaceName, modelType, validator, totalChecks);
        }
        else
        {
            AppendIndent(builder, 1, namespaceName);
            builder.Append("public global::System.Threading.Tasks.ValueTask<global::Valiant.ValidationResult> ValidateAsync(")
                .Append(modelType)
                .AppendLine(" instance, global::System.Threading.CancellationToken cancellationToken = default)");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("_ = cancellationToken;");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("return new global::System.Threading.Tasks.ValueTask<global::Valiant.ValidationResult>(Validate(instance));");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("}");

            builder.AppendLine();
            AppendIndent(builder, 1, namespaceName);
            builder.Append("public global::System.Threading.Tasks.ValueTask<bool> IsValidAsync(")
                .Append(modelType)
                .AppendLine(" instance, global::System.Threading.CancellationToken cancellationToken = default)");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("_ = cancellationToken;");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("return new global::System.Threading.Tasks.ValueTask<bool>(IsValid(instance));");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("}");
        }
    }

    private static void AppendAsyncValidationMembers(StringBuilder builder, string? namespaceName, string modelType, ValidatorModel validator, int totalChecks)
    {
        AppendIndent(builder, 1, namespaceName);
        builder.Append("public async global::System.Threading.Tasks.ValueTask<global::Valiant.ValidationResult> ValidateAsync(")
            .Append(modelType)
            .AppendLine(" instance, global::System.Threading.CancellationToken cancellationToken = default)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("cancellationToken.ThrowIfCancellationRequested();");

        if (!validator.ModelType!.IsValueType)
        {
            builder.AppendLine();
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("if (instance is null)");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, 3, namespaceName);
            builder.AppendLine("throw new global::System.ArgumentNullException(nameof(instance));");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("}");
        }

        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("global::System.Collections.Generic.List<global::Valiant.ValidationFailure>? failures = null;");

        var ruleIndex = 0;
        AppendValidationNodes(builder, namespaceName, 2, validator.Nodes, ref ruleIndex, totalChecks, true);

        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return failures is null ? global::Valiant.ValidationResult.Valid : new global::Valiant.ValidationResult(failures);");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.Append("public async global::System.Threading.Tasks.ValueTask<bool> IsValidAsync(")
            .Append(modelType)
            .AppendLine(" instance, global::System.Threading.CancellationToken cancellationToken = default)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("var result = await ValidateAsync(instance, cancellationToken).ConfigureAwait(false);");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return result.IsValid;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

    private static string GenerateDependencyInjection(ImmutableArray<ValidatorModel> validators)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("namespace Valiant.DependencyInjection");
        builder.AppendLine("{");
        builder.AppendLine("    public static class ValiantServiceCollectionExtensions");
        builder.AppendLine("    {");
        builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddValiantValidators(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (services is null)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::System.ArgumentNullException(nameof(services));");
        builder.AppendLine("            }");
        builder.AppendLine();

        foreach (var validator in validators)
        {
            builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(global::Valiant.IValidator<")
                .Append(validator.ModelType!.ToDisplayString(TypeDisplayFormat))
                .Append(">), typeof(")
                .Append(validator.ValidatorSymbol.ToDisplayString(TypeDisplayFormat))
                .AppendLine(")));");
        }

        builder.AppendLine();
        builder.AppendLine("            return services;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static bool HasAsyncChecks(ImmutableArray<ValidationNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case RuleNode ruleNode:
                    if (ruleNode.Rule.Checks.Any(static check => check.IsAsync))
                    {
                        return true;
                    }

                    break;
                case IfNode ifNode:
                    if (HasAsyncChecks(ifNode.ThenNodes) || HasAsyncChecks(ifNode.ElseNodes))
                    {
                        return true;
                    }

                    break;
                case SwitchNode switchNode:
                    foreach (var section in switchNode.Sections)
                    {
                        if (HasAsyncChecks(section.Nodes))
                        {
                            return true;
                        }
                    }

                    break;
            }
        }

        return false;
    }

    private static int CountChecks(ImmutableArray<ValidationNode> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            switch (node)
            {
                case RuleNode ruleNode:
                    count += ruleNode.Rule.Checks.Length;
                    break;
                case IfNode ifNode:
                    count += CountChecks(ifNode.ThenNodes);
                    count += CountChecks(ifNode.ElseNodes);
                    break;
                case SwitchNode switchNode:
                    foreach (var section in switchNode.Sections)
                    {
                        count += CountChecks(section.Nodes);
                    }

                    break;
            }
        }

        return count;
    }

    private static void AppendValidationNodes(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        ImmutableArray<ValidationNode> nodes,
        ref int ruleIndex,
        int totalChecks,
        bool emitAsync)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case RuleNode ruleNode:
                    AppendRule(builder, namespaceName, indentLevel, ruleNode.Rule, ref ruleIndex, totalChecks, emitAsync);
                    break;
                case IfNode ifNode:
                    AppendIfNode(builder, namespaceName, indentLevel, ifNode, ref ruleIndex, totalChecks, emitAsync);
                    break;
                case SwitchNode switchNode:
                    AppendSwitchNode(builder, namespaceName, indentLevel, switchNode, ref ruleIndex, totalChecks, emitAsync);
                    break;
            }
        }
    }

    private static void AppendRule(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        RuleModel rule,
        ref int ruleIndex,
        int totalChecks,
        bool emitAsync)
    {
        var localName = "value" + ruleIndex.ToString(CultureInfo.InvariantCulture);
        ruleIndex++;

        builder.AppendLine();
        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("var ").Append(localName).Append(" = instance.").Append(rule.PropertyAccess).AppendLine(";");

        foreach (var check in rule.Checks)
        {
            AppendCheck(builder, namespaceName, indentLevel, localName, "instance", rule, check, totalChecks, emitAsync);
        }
    }

    private static void AppendIfNode(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        IfNode node,
        ref int ruleIndex,
        int totalChecks,
        bool emitAsync)
    {
        builder.AppendLine();
        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("if (").Append(node.Condition).AppendLine(")");
        AppendValidationBlock(builder, namespaceName, indentLevel, node.ThenNodes, ref ruleIndex, totalChecks, emitAsync);

        if (!node.ElseNodes.IsDefaultOrEmpty)
        {
            AppendIndent(builder, indentLevel, namespaceName);
            builder.AppendLine("else");
            AppendValidationBlock(builder, namespaceName, indentLevel, node.ElseNodes, ref ruleIndex, totalChecks, emitAsync);
        }
    }

    private static void AppendSwitchNode(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        SwitchNode node,
        ref int ruleIndex,
        int totalChecks,
        bool emitAsync)
    {
        builder.AppendLine();
        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("switch (").Append(node.Expression).AppendLine(")");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("{");

        foreach (var section in node.Sections)
        {
            foreach (var label in section.Labels)
            {
                AppendIndent(builder, indentLevel + 1, namespaceName);
                builder.AppendLine(label);
            }

            AppendValidationNodes(builder, namespaceName, indentLevel + 2, section.Nodes, ref ruleIndex, totalChecks, emitAsync);
            AppendIndent(builder, indentLevel + 2, namespaceName);
            builder.AppendLine("break;");
        }

        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendValidationBlock(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        ImmutableArray<ValidationNode> nodes,
        ref int ruleIndex,
        int totalChecks,
        bool emitAsync)
    {
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("{");
        AppendValidationNodes(builder, namespaceName, indentLevel + 1, nodes, ref ruleIndex, totalChecks, emitAsync);
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendCheck(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        string localName,
        string instanceName,
        RuleModel rule,
        RuleCheck check,
        int totalChecks,
        bool emitAsync)
    {
        if (check.IsAsync && !emitAsync)
        {
            return;
        }

        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("if (").Append(GetFailureCondition(localName, instanceName, rule, check, emitAsync)).AppendLine(")");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, indentLevel + 1, namespaceName);
        builder.Append("(failures ??= new global::System.Collections.Generic.List<global::Valiant.ValidationFailure>(")
            .Append(totalChecks.ToString(CultureInfo.InvariantCulture))
            .Append(")).Add(new global::Valiant.ValidationFailure(")
            .Append(ToLiteral(rule.PropertyName))
            .Append(", ")
            .Append(ToLiteral(check.Kind))
            .Append(", ")
            .Append(ToLiteral(check.Message ?? GetDefaultMessage(rule, check)))
            .AppendLine("));");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("}");
    }

    private static string GetFailureCondition(string localName, string instanceName, RuleModel rule, RuleCheck check, bool emitAsync)
    {
        switch (check.Kind)
        {
            case "NotNull":
                return localName + " is null";
            case "Null":
                return localName + " is not null";
            case "NotEmpty":
                if (rule.IsString)
                {
                    return "global::System.String.IsNullOrWhiteSpace(" + localName + ")";
                }

                return "global::System.Collections.Generic.EqualityComparer<" + rule.PropertyType + ">.Default.Equals(" + localName + ", default!)";
            case "Empty":
                if (rule.IsString)
                {
                    return "!" + "global::System.String.IsNullOrEmpty(" + localName + ")";
                }

                return "!global::System.Collections.Generic.EqualityComparer<" + rule.PropertyType + ">.Default.Equals(" + localName + ", default!)";
            case "Length":
                return localName + " is not null && (" + localName + ".Length < " + check.Arguments[0] + " || " + localName + ".Length > " + check.Arguments[1] + ")";
            case "MinimumLength":
                return localName + " is not null && " + localName + ".Length < " + check.Arguments[0];
            case "MaximumLength":
                return localName + " is not null && " + localName + ".Length > " + check.Arguments[0];
            case "GreaterThan":
                return "global::System.Collections.Generic.Comparer<" + rule.PropertyType + ">.Default.Compare(" + localName + ", " + check.Arguments[0] + ") <= 0";
            case "GreaterThanOrEqualTo":
                return "global::System.Collections.Generic.Comparer<" + rule.PropertyType + ">.Default.Compare(" + localName + ", " + check.Arguments[0] + ") < 0";
            case "LessThan":
                return "global::System.Collections.Generic.Comparer<" + rule.PropertyType + ">.Default.Compare(" + localName + ", " + check.Arguments[0] + ") >= 0";
            case "LessThanOrEqualTo":
                return "global::System.Collections.Generic.Comparer<" + rule.PropertyType + ">.Default.Compare(" + localName + ", " + check.Arguments[0] + ") > 0";
            case "Equal":
                return "!global::System.Collections.Generic.EqualityComparer<" + rule.PropertyType + ">.Default.Equals(" + localName + ", " + check.Arguments[0] + ")";
            case "NotEqual":
                return "global::System.Collections.Generic.EqualityComparer<" + rule.PropertyType + ">.Default.Equals(" + localName + ", " + check.Arguments[0] + ")";
            case "InclusiveBetween":
                return "global::System.Collections.Generic.Comparer<" + rule.PropertyType + ">.Default.Compare(" + localName + ", " + check.Arguments[0] + ") < 0 || global::System.Collections.Generic.Comparer<" + rule.PropertyType + ">.Default.Compare(" + localName + ", " + check.Arguments[1] + ") > 0";
            case "ExclusiveBetween":
                return "global::System.Collections.Generic.Comparer<" + rule.PropertyType + ">.Default.Compare(" + localName + ", " + check.Arguments[0] + ") <= 0 || global::System.Collections.Generic.Comparer<" + rule.PropertyType + ">.Default.Compare(" + localName + ", " + check.Arguments[1] + ") >= 0";
            case "Must":
                return "!" + GetCustomPredicateInvocation(localName, instanceName, check, false);
            case "MustAsync":
                if (!emitAsync)
                {
                    throw new InvalidOperationException("Cannot emit async custom validation in a synchronous method.");
                }

                return "!" + GetCustomPredicateInvocation(localName, instanceName, check, true);
            default:
                throw new InvalidOperationException("Unsupported check kind '" + check.Kind + "'.");
        }
    }

    private static string GetCustomPredicateInvocation(string localName, string instanceName, RuleCheck check, bool emitAsync)
    {
        if (check.DelegateType is null || check.PredicateParameterCount == 0)
        {
            throw new InvalidOperationException("Custom validation check is missing delegate metadata.");
        }

        var invocation = "((" + check.DelegateType + ")(" + GetCustomPredicateExpression(check) + "))(" + GetCustomPredicateArguments(localName, instanceName, check) + ")";
        return emitAsync
            ? "(await " + invocation + ".ConfigureAwait(false))"
            : invocation;
    }

    private static string GetCustomPredicateExpression(RuleCheck check)
    {
        var predicate = check.Arguments[0];
        if (check.PredicateLineNumber <= 0 || string.IsNullOrWhiteSpace(check.PredicateFilePath))
        {
            return predicate;
        }

        return "\n#line " + check.PredicateLineNumber.ToString(CultureInfo.InvariantCulture) + " \"" + EscapeLineDirectivePath(check.PredicateFilePath!) + "\"\n" +
            predicate + "\n" +
            "#line default\n";
    }

    private static string EscapeLineDirectivePath(string path)
    {
        return path.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string GetCustomPredicateArguments(string localName, string instanceName, RuleCheck check)
    {
        switch (check.PredicateParameterCount)
        {
            case 1:
                return localName;
            case 2 when check.IsAsync:
                return localName + ", cancellationToken";
            case 2:
                return instanceName + ", " + localName;
            case 3:
                return instanceName + ", " + localName + ", cancellationToken";
            default:
                throw new InvalidOperationException("Unsupported custom validation delegate arity '" + check.PredicateParameterCount.ToString(CultureInfo.InvariantCulture) + "'.");
        }
    }

    private static string GetDefaultMessage(RuleModel rule, RuleCheck check)
    {
        switch (check.Kind)
        {
            case "NotNull":
                return "'" + rule.PropertyName + "' must not be null.";
            case "Null":
                return "'" + rule.PropertyName + "' must be null.";
            case "NotEmpty":
                return "'" + rule.PropertyName + "' must not be empty.";
            case "Empty":
                return "'" + rule.PropertyName + "' must be empty.";
            case "Length":
                return "'" + rule.PropertyName + "' must be between " + check.Arguments[0] + " and " + check.Arguments[1] + " characters.";
            case "MinimumLength":
                return "'" + rule.PropertyName + "' must be at least " + check.Arguments[0] + " characters.";
            case "MaximumLength":
                return "'" + rule.PropertyName + "' must be at most " + check.Arguments[0] + " characters.";
            case "GreaterThan":
                return "'" + rule.PropertyName + "' must be greater than " + check.Arguments[0] + ".";
            case "GreaterThanOrEqualTo":
                return "'" + rule.PropertyName + "' must be greater than or equal to " + check.Arguments[0] + ".";
            case "LessThan":
                return "'" + rule.PropertyName + "' must be less than " + check.Arguments[0] + ".";
            case "LessThanOrEqualTo":
                return "'" + rule.PropertyName + "' must be less than or equal to " + check.Arguments[0] + ".";
            case "Equal":
                return "'" + rule.PropertyName + "' must be equal to " + check.Arguments[0] + ".";
            case "NotEqual":
                return "'" + rule.PropertyName + "' must not be equal to " + check.Arguments[0] + ".";
            case "InclusiveBetween":
                return "'" + rule.PropertyName + "' must be between " + check.Arguments[0] + " and " + check.Arguments[1] + ".";
            case "ExclusiveBetween":
                return "'" + rule.PropertyName + "' must be between " + check.Arguments[0] + " and " + check.Arguments[1] + ", exclusive.";
            case "Must":
            case "MustAsync":
                return "'" + rule.PropertyName + "' did not pass the custom validation predicate.";
            default:
                throw new InvalidOperationException("Unsupported check kind '" + check.Kind + "'.");
        }
    }

    private static bool TryParseRule(
        InvocationExpressionSyntax invocation,
        string contextParameterName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out RuleModel rule,
        out DiagnosticInfo? diagnostic)
    {
        rule = null!;
        diagnostic = null;

        if (!TryCollectChain(invocation, out var calls) ||
            calls.Count < 2 ||
            calls[0].Name != "RuleFor" ||
            !IsIdentifier(calls[0].Receiver, contextParameterName))
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedRuleSyntax,
                invocation.GetLocation(),
                "Rules must start with a RuleFor call on the validation context parameter.");
            return false;
        }

        if (calls[0].Arguments.Count != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedRuleSyntax,
                calls[0].Invocation.GetLocation(),
                "RuleFor must have exactly one property selector argument.");
            return false;
        }

        if (!TryParsePropertySelector(calls[0].Arguments[0].Expression, semanticModel, cancellationToken, out var property, out diagnostic))
        {
            return false;
        }

        var checks = ImmutableArray.CreateBuilder<RuleCheck>();
        for (var index = 1; index < calls.Count; index++)
        {
            var call = calls[index];
            if (call.Name == "WithMessage")
            {
                if (checks.Count == 0)
                {
                    diagnostic = new DiagnosticInfo(
                        UnsupportedRuleSyntax,
                        call.Invocation.GetLocation(),
                        "WithMessage must follow a validator call.");
                    return false;
                }

                if (call.Arguments.Count != 1 ||
                    !TryGetConstantString(call.Arguments[0].Expression, semanticModel, cancellationToken, out var message))
                {
                    diagnostic = new DiagnosticInfo(
                        UnsupportedRuleSyntax,
                        call.Invocation.GetLocation(),
                        "WithMessage currently requires one constant string argument.");
                    return false;
                }

                checks[checks.Count - 1].Message = message;
                continue;
            }

            if (!TryCreateCheck(call, contextParameterName, semanticModel, cancellationToken, out var check, out diagnostic))
            {
                return false;
            }

            checks.Add(check);
        }

        if (checks.Count == 0)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedRuleSyntax,
                invocation.GetLocation(),
                "RuleFor must include at least one validator call.");
            return false;
        }

        rule = new RuleModel(
            property.PropertyName,
            property.PropertyAccess,
            property.PropertyType,
            property.IsString,
            checks.ToImmutable());
        return true;
    }

    private static bool TryCreateCheck(
        CallInfo call,
        string contextParameterName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out RuleCheck check,
        out DiagnosticInfo? diagnostic)
    {
        check = null!;
        diagnostic = null;

        if (call.Name is "Must" or "MustAsync")
        {
            return TryCreateCustomCheck(call, contextParameterName, semanticModel, cancellationToken, out check, out diagnostic);
        }

        var expectedArguments = GetExpectedArgumentCount(call.Name);
        if (expectedArguments < 0)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedRuleSyntax,
                call.Invocation.GetLocation(),
                "Validator call '" + call.Name + "' is not supported yet.");
            return false;
        }

        if (call.Arguments.Count != expectedArguments)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedRuleSyntax,
                call.Invocation.GetLocation(),
                "Validator call '" + call.Name + "' expects " + expectedArguments.ToString(CultureInfo.InvariantCulture) + " argument(s).");
            return false;
        }

        var arguments = ImmutableArray.CreateBuilder<string>();
        foreach (var argument in call.Arguments)
        {
            arguments.Add(RewriteForValidation(argument.Expression, contextParameterName));
        }

        check = new RuleCheck(call.Name, arguments.ToImmutable(), false, null, 0, null, 0);
        return true;
    }

    private static bool TryCreateCustomCheck(
        CallInfo call,
        string contextParameterName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out RuleCheck check,
        out DiagnosticInfo? diagnostic)
    {
        check = null!;
        diagnostic = null;

        if (call.Arguments.Count != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedRuleSyntax,
                call.Invocation.GetLocation(),
                "Validator call '" + call.Name + "' expects one predicate argument.");
            return false;
        }

        var method = semanticModel.GetSymbolInfo(call.Invocation, cancellationToken).Symbol as IMethodSymbol;
        if (method is null || method.Parameters.Length != 1)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedRuleSyntax,
                call.Invocation.GetLocation(),
                "Valiant could not determine the custom validation predicate overload.");
            return false;
        }

        var delegateType = method.Parameters[0].Type as INamedTypeSymbol;
        var invokeMethod = delegateType?.DelegateInvokeMethod;
        if (delegateType is null || invokeMethod is null)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedRuleSyntax,
                call.Invocation.GetLocation(),
                "Custom validation predicates must be delegate-compatible lambdas or method groups.");
            return false;
        }

        var parameterCount = invokeMethod.Parameters.Length;
        if (call.Name == "Must" && parameterCount is not 1 and not 2)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedRuleSyntax,
                call.Invocation.GetLocation(),
                "Must supports property-only predicates or model-and-property predicates.");
            return false;
        }

        if (call.Name == "MustAsync" && parameterCount is not 2 and not 3)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedRuleSyntax,
                call.Invocation.GetLocation(),
                "MustAsync supports property-and-cancellation-token predicates or model-property-and-cancellation-token predicates.");
            return false;
        }

        var predicateExpression = call.Arguments[0].Expression;
        var predicate = RewriteForValidation(predicateExpression, contextParameterName);
        var lineSpan = predicateExpression.GetLocation().GetLineSpan();
        var filePath = string.IsNullOrWhiteSpace(lineSpan.Path)
            ? predicateExpression.SyntaxTree.FilePath
            : lineSpan.Path;
        var arguments = ImmutableArray.Create(predicate);
        check = new RuleCheck(
            call.Name,
            arguments,
            call.Name == "MustAsync",
            delegateType.ToDisplayString(TypeDisplayFormat),
            parameterCount,
            filePath,
            lineSpan.StartLinePosition.Line + 1);
        return true;
    }

    private static int GetExpectedArgumentCount(string callName)
    {
        switch (callName)
        {
            case "NotNull":
            case "Null":
            case "NotEmpty":
            case "Empty":
                return 0;
            case "MinimumLength":
            case "MaximumLength":
            case "GreaterThan":
            case "GreaterThanOrEqualTo":
            case "LessThan":
            case "LessThanOrEqualTo":
            case "Equal":
            case "NotEqual":
                return 1;
            case "Length":
            case "InclusiveBetween":
            case "ExclusiveBetween":
                return 2;
            default:
                return -1;
        }
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

        if (lambda is null || lambda.Body is not ExpressionSyntax body)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedRuleSyntax,
                expression.GetLocation(),
                "RuleFor currently requires a simple property lambda such as x => x.Name.");
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
                UnsupportedRuleSyntax,
                expression.GetLocation(),
                "RuleFor currently requires exactly one lambda parameter.");
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
                UnsupportedRuleSyntax,
                expression.GetLocation(),
                "RuleFor currently supports direct property access, for example x => x.Name or x => x.Address.City.");
            return false;
        }

        var type = semanticModel.GetTypeInfo(body, cancellationToken).Type;
        if (type is null)
        {
            diagnostic = new DiagnosticInfo(
                UnsupportedRuleSyntax,
                expression.GetLocation(),
                "Valiant could not determine the selected property type.");
            return false;
        }

        selector = new PropertySelector(
            string.Join(".", displaySegments),
            string.Join(".", accessSegments),
            type.ToDisplayString(TypeDisplayFormat),
            type.SpecialType == SpecialType.System_String);
        return true;
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
        var rewritten = new ValidationContextRewriter(contextParameterName).Visit(node);
        return rewritten?.ToString() ?? node.ToString();
    }

    private static bool TryCollectChain(InvocationExpressionSyntax invocation, out List<CallInfo> calls)
    {
        calls = [];
        ExpressionSyntax current = invocation;

        while (current is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } currentInvocation)
        {
            calls.Add(new CallInfo(
                memberAccess.Name.Identifier.ValueText,
                memberAccess.Expression,
                currentInvocation.ArgumentList.Arguments,
                currentInvocation));

            current = memberAccess.Expression;
        }

        calls.Reverse();
        return calls.Count > 0;
    }

    private static bool TryGetConstantString(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken cancellationToken, out string value)
    {
        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (constant is { HasValue: true, Value: string stringValue })
        {
            value = stringValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool IsIdentifier(ExpressionSyntax expression, string name)
    {
        return expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == name;
    }

    private static MethodDeclarationSyntax? FindRulesMethod(INamedTypeSymbol validatorSymbol, string rulesMethodName, CancellationToken cancellationToken)
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
                if (method.Identifier.ValueText == rulesMethodName)
                {
                    return method;
                }
            }
        }

        return null;
    }

    private static string GetRulesMethodName(AttributeData attribute)
    {
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (namedArgument is { Key: "RulesMethod", Value.Value: string methodName } &&
                !string.IsNullOrWhiteSpace(methodName))
            {
                return methodName;
            }
        }

        return "Configure";
    }

    private static string GetTypeModifier(INamedTypeSymbol symbol)
    {
        var builder = new StringBuilder();

        switch (symbol.DeclaredAccessibility)
        {
            case Accessibility.Public:
                builder.Append("public ");
                break;
            case Accessibility.Internal:
                builder.Append("internal ");
                break;
            case Accessibility.Private:
                builder.Append("private ");
                break;
            case Accessibility.Protected:
                builder.Append("protected ");
                break;
            case Accessibility.ProtectedAndInternal:
                builder.Append("private protected ");
                break;
            case Accessibility.ProtectedOrInternal:
                builder.Append("protected internal ");
                break;
        }

        if (symbol.IsSealed)
        {
            builder.Append("sealed ");
        }

        return builder.ToString();
    }

    private static void AppendIndent(StringBuilder builder, int level, string? namespaceName)
    {
        var adjustedLevel = namespaceName is null ? level : level + 1;
        builder.Append(' ', adjustedLevel * 4);
    }

    private static string GetHintName(INamedTypeSymbol symbol, string suffix)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty)
            .Replace('.', '_')
            .Replace('+', '_') + "." + suffix + ".g.cs";
    }

    private static string ToLiteral(string value)
    {
        return SymbolDisplay.FormatLiteral(value, true);
    }

    private sealed class ValidatorModel(
        INamedTypeSymbol validatorSymbol,
        ITypeSymbol? modelType,
        string rulesMethodName,
        ImmutableArray<ValidationNode> nodes,
        ImmutableArray<DiagnosticInfo> diagnostics)
    {
        public INamedTypeSymbol ValidatorSymbol { get; } = validatorSymbol;

        public ITypeSymbol? ModelType { get; } = modelType;

        public string RulesMethodName { get; } = rulesMethodName;

        public ImmutableArray<ValidationNode> Nodes { get; } = nodes;

        public ImmutableArray<DiagnosticInfo> Diagnostics { get; } = diagnostics;
    }

    private abstract class ValidationNode
    {
    }

    private sealed class RuleNode(RuleModel rule) : ValidationNode
    {
        public RuleModel Rule { get; } = rule;
    }

    private sealed class IfNode(
        string condition,
        ImmutableArray<ValidationNode> thenNodes,
        ImmutableArray<ValidationNode> elseNodes) : ValidationNode
    {
        public string Condition { get; } = condition;

        public ImmutableArray<ValidationNode> ThenNodes { get; } = thenNodes;

        public ImmutableArray<ValidationNode> ElseNodes { get; } = elseNodes;
    }

    private sealed class SwitchNode(
        string expression,
        ImmutableArray<SwitchSectionModel> sections) : ValidationNode
    {
        public string Expression { get; } = expression;

        public ImmutableArray<SwitchSectionModel> Sections { get; } = sections;
    }

    private sealed class SwitchSectionModel(
        ImmutableArray<string> labels,
        ImmutableArray<ValidationNode> nodes)
    {
        public ImmutableArray<string> Labels { get; } = labels;

        public ImmutableArray<ValidationNode> Nodes { get; } = nodes;
    }

    private sealed class RuleModel(
        string propertyName,
        string propertyAccess,
        string propertyType,
        bool isString,
        ImmutableArray<RuleCheck> checks)
    {
        public string PropertyName { get; } = propertyName;

        public string PropertyAccess { get; } = propertyAccess;

        public string PropertyType { get; } = propertyType;

        public bool IsString { get; } = isString;

        public ImmutableArray<RuleCheck> Checks { get; } = checks;
    }

    private sealed class RuleCheck(
        string kind,
        ImmutableArray<string> arguments,
        bool isAsync,
        string? delegateType,
        int predicateParameterCount,
        string? predicateFilePath,
        int predicateLineNumber)
    {
        public string Kind { get; } = kind;

        public ImmutableArray<string> Arguments { get; } = arguments;

        public bool IsAsync { get; } = isAsync;

        public string? DelegateType { get; } = delegateType;

        public int PredicateParameterCount { get; } = predicateParameterCount;

        public string? PredicateFilePath { get; } = predicateFilePath;

        public int PredicateLineNumber { get; } = predicateLineNumber;

        public string? Message { get; set; }
    }

    private sealed class PropertySelector(
        string propertyName,
        string propertyAccess,
        string propertyType,
        bool isString)
    {
        public string PropertyName { get; } = propertyName;

        public string PropertyAccess { get; } = propertyAccess;

        public string PropertyType { get; } = propertyType;

        public bool IsString { get; } = isString;
    }

    private sealed class CallInfo(
        string name,
        ExpressionSyntax receiver,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        InvocationExpressionSyntax invocation)
    {
        public string Name { get; } = name;

        public ExpressionSyntax Receiver { get; } = receiver;

        public SeparatedSyntaxList<ArgumentSyntax> Arguments { get; } = arguments;

        public InvocationExpressionSyntax Invocation { get; } = invocation;
    }

    private sealed class ValidationContextRewriter(string contextParameterName) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (IsIdentifier(node.Expression, contextParameterName) &&
                node.Name.Identifier.ValueText == "Instance")
            {
                return SyntaxFactory.IdentifierName("instance").WithTriviaFrom(node);
            }

            return base.VisitMemberAccessExpression(node);
        }
    }

    private sealed class DiagnosticInfo(
        DiagnosticDescriptor descriptor,
        Location? location,
        params object[] messageArgs)
    {
        public DiagnosticDescriptor Descriptor { get; } = descriptor;

        public Location? Location { get; } = location;

        public object[] MessageArgs { get; } = messageArgs;

        public Diagnostic Create()
        {
            return Diagnostic.Create(Descriptor, Location, MessageArgs);
        }
    }
}
