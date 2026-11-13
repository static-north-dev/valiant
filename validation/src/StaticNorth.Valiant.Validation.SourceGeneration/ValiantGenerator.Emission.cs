using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static StaticNorth.Valiant.SourceGeneration.ValiantSourceBuilder;

namespace StaticNorth.Valiant.Validation.SourceGeneration;

/// <summary>
/// Generates Valiant validation implementations and dependency injection registration code.
/// </summary>
public sealed partial class ValiantGenerator
{
    private static void ExecuteValidator(SourceProductionContext context, ValidatorModel validator)
    {
        foreach (var diagnostic in validator.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.Create());
        }

        if (!IsValidValidator(validator))
        {
            return;
        }

        context.AddSource(validator.Declaration.HintName, SourceText.From(GenerateValidator(validator), Encoding.UTF8));
    }

    private static void ExecuteDependencyInjection(
        SourceProductionContext context,
        bool hasDependencyInjection,
        ImmutableArray<DependencyInjectionValidatorModel> validators,
        ImmutableArray<DependencyInjectionPropertyValidatorModel> propertyValidators)
    {
        if (!hasDependencyInjection)
        {
            return;
        }

        context.AddSource("StaticNorth.Valiant.Validation.DependencyInjection.g.cs", SourceText.From(GenerateDependencyInjection(validators, propertyValidators), Encoding.UTF8));
    }

    private static void ExecuteDuplicateValidatorModelDiagnostics(
        SourceProductionContext context,
        ImmutableArray<ValidatorModel> validators)
    {
        var validValidators = validators
            .Where(static validator => IsValidValidator(validator) && validator.RegisterAsDefault)
            .GroupBy(static validator => validator.ModelType, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1);

        foreach (var group in validValidators)
        {
            var orderedValidators = group
                .GroupBy(static validator => validator.Declaration.Type, StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static validator => validator.Declaration.Name, StringComparer.Ordinal)
                .ThenBy(static validator => validator.Declaration.HintName, StringComparer.Ordinal)
                .ToImmutableArray();
            if (orderedValidators.Length < 2)
            {
                continue;
            }

            var primaryValidator = orderedValidators[0];
            for (var index = 1; index < orderedValidators.Length; index++)
            {
                var duplicateValidator = orderedValidators[index];
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateValidatorModel,
                    duplicateValidator.Declaration.Location.ToLocation(),
                    duplicateValidator.Declaration.Name,
                    duplicateValidator.ModelType,
                    primaryValidator.Declaration.Name));
            }
        }
    }

    private static string GenerateValidator(ValidatorModel validator)
    {
        var builder = new StringBuilder();
        var declaration = validator.Declaration;
        var namespaceName = declaration.NamespaceName;
        var modelType = validator.ModelType!;
        var modifier = declaration.Modifier;

        AppendGeneratedFileHeader(builder);
        if (!validator.Usings.IsDefaultOrEmpty)
        {
            foreach (var usingDirective in validator.Usings)
            {
                builder.AppendLine(usingDirective);
            }

            builder.AppendLine();
        }

        if (namespaceName is not null)
        {
            builder.Append("namespace ").Append(namespaceName).AppendLine();
            builder.AppendLine("{");
        }

        var containingTypeCount = AppendContainingTypeDeclarations(
            builder,
            namespaceName,
            declaration.ContainingTypes,
            static containingType => containingType.Modifier,
            static containingType => containingType.Keyword,
            static containingType => containingType.Name);

        AppendXmlDocumentation(builder, namespaceName, 0, "Validates instances using the rules declared for this validator.");
        AppendIndent(builder, 0, namespaceName);
        builder.Append(modifier).Append("partial class ").Append(declaration.Name)
            .Append(" : global::StaticNorth.Valiant.Validation.IValidator<").Append(modelType).AppendLine(">");
        AppendIndent(builder, 0, namespaceName);
        builder.AppendLine("{");

        AppendValidationOptionsMembers(builder, namespaceName, validator);
        if (validator.ImplementsValidatorDefinition && !validator.InheritsValidatorBase)
        {
            AppendValidatorDefinitionImplementation(builder, namespaceName, modelType, validator);
        }

        if (validator.UsesRuntimeValidation)
        {
            AppendFluentValidationMethods(builder, namespaceName, modelType, validator);
        }
        else
        {
            AppendGeneratedValidationMethods(builder, namespaceName, modelType, validator);
        }

        if (validator.HasConfigureMethod)
        {
            AppendConfigureValidationMethod(builder, namespaceName, modelType, validator);
        }

        AppendStatusMethods(builder, namespaceName, modelType, validator);
        AppendEffectiveFailureBehaviorMember(builder, namespaceName);
        AppendIndent(builder, 0, namespaceName);
        builder.AppendLine("}");
        AppendContainingTypeClosures(builder, namespaceName, containingTypeCount);
        AppendValidationMetadataProvider(builder, namespaceName, validator);

        if (namespaceName is not null)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static void AppendConstructorXmlDocumentation(
        StringBuilder builder,
        string? namespaceName,
        ConstructorModel constructor)
    {
        var parameters = constructor.Parameters
            .Select(static parameter => (parameter.Name, "A constructor dependency passed through to the user-defined validator constructor."))
            .ToArray();

        AppendXmlDocumentation(builder, namespaceName, 1, "Initializes a new validator instance.", null, parameters);
    }

    private static void AppendValidationOptionsMembers(
        StringBuilder builder,
        string? namespaceName,
        ValidatorModel validator)
    {
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private readonly global::StaticNorth.Valiant.Validation.ValiantValidationOptions? _valiantValidationOptions = null;");
        builder.AppendLine();

        var declaration = validator.Declaration;
        var constructor = declaration.Constructor;
        if (constructor is null)
        {
            AppendXmlDocumentation(builder, namespaceName, 1, "Initializes a new validator instance.");
            AppendIndent(builder, 1, namespaceName);
            builder.Append("public ").Append(declaration.Name).AppendLine("()");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("}");
            builder.AppendLine();

            AppendXmlDocumentation(
                builder,
                namespaceName,
                1,
                "Initializes a new validator instance with explicit validation options.",
                null,
                ("valiantValidationOptions", "The validation options to use for this validator instance."));
            AppendIndent(builder, 1, namespaceName);
            builder.Append("public ").Append(declaration.Name).AppendLine("(global::StaticNorth.Valiant.Validation.ValiantValidationOptions valiantValidationOptions)");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("_valiantValidationOptions = valiantValidationOptions ?? throw new global::System.ArgumentNullException(nameof(valiantValidationOptions));");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("}");
            builder.AppendLine();
            return;
        }

        AppendConstructorXmlDocumentation(builder, namespaceName, constructor);
        AppendIndent(builder, 1, namespaceName);
        builder.Append("public ").Append(declaration.Name).Append('(');
        for (var index = 0; index < constructor.Parameters.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            var parameter = constructor.Parameters[index];
            builder.Append(parameter.Type).Append(' ').Append(parameter.Name);
        }

        if (constructor.Parameters.Length > 0)
        {
            builder.Append(", ");
        }

        builder.Append("global::StaticNorth.Valiant.Validation.ValiantValidationOptions valiantValidationOptions) : this(");
        for (var index = 0; index < constructor.Parameters.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(constructor.Parameters[index].Name);
        }

        builder.AppendLine(")");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("_valiantValidationOptions = valiantValidationOptions ?? throw new global::System.ArgumentNullException(nameof(valiantValidationOptions));");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendValidatorDefinitionImplementation(
        StringBuilder builder,
        string? namespaceName,
        string modelType,
        ValidatorModel validator)
    {
        AppendIndent(builder, 1, namespaceName);
        builder.Append("void global::StaticNorth.Valiant.Validation.IValidatorDefinition<")
            .Append(modelType)
            .Append(">.Configure(global::StaticNorth.Valiant.Validation.ValidationContext<")
            .Append(modelType)
            .AppendLine("> context)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.Append(validator.ConfigureMethodName).AppendLine("(context);");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static IMethodSymbol? SelectConstructorToChain(INamedTypeSymbol validatorSymbol)
    {
        return validatorSymbol.InstanceConstructors
            .Where(static constructor => !constructor.IsImplicitlyDeclared && constructor.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(static constructor => constructor.Parameters.Length)
            .FirstOrDefault();
    }

    private static void AppendStatusMethods(
        StringBuilder builder,
        string? namespaceName,
        string modelType,
        ValidatorModel validator)
    {
        builder.AppendLine();
        AppendXmlDocumentation(
            builder,
            namespaceName,
            1,
            "Determines whether the specified instance is valid.",
            "True when the specified instance is valid; otherwise, false.",
            ("instance", "The instance to validate."));
        AppendIndent(builder, 1, namespaceName);
        builder.Append("public bool IsValid(").Append(modelType).Append(" instance) => ");
        builder.AppendLine(validator.UsesRuntimeValidation && !HasAsyncChecks(validator.Nodes)
            ? "ConfigureValidation(instance).ToStatus();"
            : "Validate(instance).IsValid;");

        builder.AppendLine();
        AppendXmlDocumentation(
            builder,
            namespaceName,
            1,
            "Asynchronously determines whether the specified instance is valid.",
            "A value task containing true when the specified instance is valid; otherwise, false.",
            ("instance", "The instance to validate."),
            ("cancellationToken", "A cancellation token used to cancel asynchronous validation."));
        AppendIndent(builder, 1, namespaceName);
        builder.Append("public async global::System.Threading.Tasks.ValueTask<bool> IsValidAsync(")
            .Append(modelType)
            .AppendLine(" instance, global::System.Threading.CancellationToken cancellationToken = default)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        if (validator.UsesRuntimeValidation)
        {
            builder.AppendLine("cancellationToken.ThrowIfCancellationRequested();");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("return await ConfigureValidation(instance).ToStatusAsync(cancellationToken).ConfigureAwait(false);");
        }
        else
        {
            builder.AppendLine("var result = await ValidateAsync(instance, cancellationToken).ConfigureAwait(false);");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("return result.IsValid;");
        }

        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendEffectiveFailureBehaviorMember(StringBuilder builder, string? namespaceName)
    {
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private global::StaticNorth.Valiant.Validation.ValiantFailureBehavior GetEffectiveFailureBehavior()");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return _valiantValidationOptions?.FailureBehavior ?? global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.ValidateAll;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendFluentValidationMethods(
        StringBuilder builder,
        string? namespaceName,
        string modelType,
        ValidatorModel validator)
    {
        // Configure can run user code while declaring rules. Every mode must use the same
        // declaration and execution order, including code that throws during declaration.
        AppendValidateMethodStart(builder, namespaceName, modelType, emitAsync: false);
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine(HasAsyncChecks(validator.Nodes)
            ? "throw new global::System.InvalidOperationException(\"This validator contains asynchronous validations. Call ValidateAsync instead.\");"
            : "return ConfigureValidation(instance).ToResult();");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        AppendValidateMethodStart(builder, namespaceName, modelType, emitAsync: true, awaitResult: true);
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return await ConfigureValidation(instance).ToResultAsync(cancellationToken).ConfigureAwait(false);");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendConfigureValidationMethod(
        StringBuilder builder,
        string? namespaceName,
        string modelType,
        ValidatorModel validator)
    {
        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.Append("private global::StaticNorth.Valiant.Validation.ValidationContext<")
            .Append(modelType).Append("> ConfigureValidation(").Append(modelType).AppendLine(" instance)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendRuntimeNullCheck(builder, namespaceName, validator);
        AppendIndent(builder, 2, namespaceName);
        builder.Append("var context = new global::StaticNorth.Valiant.Validation.ValidationContext<").Append(modelType).AppendLine(">(instance);");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (GetEffectiveFailureBehavior() == global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("context.FailFast();");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, 2, namespaceName);
        AppendConfigureInvocation(builder, modelType, validator);
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return context;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendConfigureInvocation(
        StringBuilder builder,
        string modelType,
        ValidatorModel validator)
    {
        if (validator.ImplementsValidatorDefinition && !validator.InheritsValidatorBase)
        {
            builder.Append("((global::StaticNorth.Valiant.Validation.IValidatorDefinition<")
                .Append(modelType)
                .AppendLine(">)this).Configure(context);");
            return;
        }

        builder.Append(validator.ConfigureMethodName).AppendLine("(context);");
    }

    private static void AppendRuntimeNullCheck(StringBuilder builder, string? namespaceName, ValidatorModel validator)
    {
        if (validator.IsModelValueType)
        {
            return;
        }

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

    private static void AppendValidationMetadataProvider(
        StringBuilder builder,
        string? namespaceName,
        ValidatorModel validator)
    {
        builder.AppendLine();
        AppendIndent(builder, 0, namespaceName);
        builder.Append("internal sealed class ").Append(GetMetadataProviderTypeName(validator.Declaration.Type))
            .AppendLine(" : global::StaticNorth.Valiant.Validation.IValiantValidationMetadataProvider");
        AppendIndent(builder, 0, namespaceName);
        builder.AppendLine("{");

        AppendIndent(builder, 1, namespaceName);
        builder.Append("public global::System.Type ModelType => typeof(")
            .Append(validator.MetadataModelType!)
            .AppendLine(");");
        builder.AppendLine();

        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("public global::System.Collections.Generic.IReadOnlyList<global::StaticNorth.Valiant.Validation.ValiantPropertyValidationMetadata> Properties { get; } = new global::StaticNorth.Valiant.Validation.ValiantPropertyValidationMetadata[]");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendValidationMetadataNodes(builder, namespaceName, 2, validator.Nodes, isConditional: false);
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("};");

        AppendIndent(builder, 0, namespaceName);
        builder.AppendLine("}");
    }

    private static string GetMetadataProviderTypeName(string validatorType)
    {
        return "__Valiant" +
            validatorType
                .Replace("global::", string.Empty)
                .Replace('.', '_')
                .Replace('+', '_')
            + "MetadataProvider";
    }

    private static void AppendValidationMetadataNodes(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        ImmutableArray<ValidationNode> nodes,
        bool isConditional)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case RuleNode ruleNode:
                    AppendRuleMetadata(builder, namespaceName, indentLevel, ruleNode.Rule, isConditional || ruleNode.Rule.ConditionExpression is not null);
                    break;
                case IfNode ifNode:
                    AppendValidationMetadataNodes(builder, namespaceName, indentLevel, ifNode.ThenNodes, isConditional: true);
                    AppendValidationMetadataNodes(builder, namespaceName, indentLevel, ifNode.ElseNodes, isConditional: true);
                    break;
                case SwitchNode switchNode:
                    foreach (var section in switchNode.Sections)
                    {
                        AppendValidationMetadataNodes(builder, namespaceName, indentLevel, section.Nodes, isConditional: true);
                    }

                    break;
                case LoopNode loopNode:
                    AppendValidationMetadataNodes(builder, namespaceName, indentLevel, loopNode.Nodes, isConditional: true);
                    break;
                case CollectionIterationNode iterationNode:
                    AppendValidationMetadataNodes(builder, namespaceName, indentLevel, iterationNode.Nodes, isConditional: true);
                    break;
                case ChildPropertyValidationNode childNode:
                    AppendValidationMetadataNodes(builder, namespaceName, indentLevel, childNode.Nodes, isConditional: true);
                    break;
                case ValidatorReuseNode:
                case FailureBehaviorNode:
                case FlowControlNode:
                case RawStatementNode:
                    break;
            }
        }
    }

    private static void AppendRuleMetadata(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        RuleModel rule,
        bool isConditional)
    {
        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("new global::StaticNorth.Valiant.Validation.ValiantPropertyValidationMetadata(")
            .Append(ToLiteral(rule.PropertyName))
            .Append(", typeof(")
            .Append(rule.MetadataPropertyType)
            .Append("), ")
            .Append(isConditional ? "true" : "false")
            .AppendLine(", new global::StaticNorth.Valiant.Validation.ValiantValidationCheckMetadata[]");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("{");

        foreach (var check in rule.Checks)
        {
            AppendCheckMetadata(builder, namespaceName, indentLevel + 1, check, isConditional);
        }

        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("}),");
    }

    private static void AppendCheckMetadata(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        RuleCheck check,
        bool isConditional)
    {
        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("new global::StaticNorth.Valiant.Validation.ValiantValidationCheckMetadata(")
            .Append(ToLiteral(check.Kind))
            .Append(", ")
            .Append(ToStringArrayExpression(check.Arguments))
            .Append(", ")
            .Append(ToStringArrayExpression(check.TypeArguments))
            .Append(", ")
            .Append(GetMetadataLiteralExpression(check.Code))
            .Append(", ")
            .Append(GetMetadataLiteralExpression(check.Message))
            .Append(", ")
            .Append(GetMetadataSeverityExpression(check.Severity))
            .Append(", ")
            .Append(check.IsAsync ? "true" : "false")
            .Append(", ")
            .Append(isConditional ? "true" : "false")
            .AppendLine("),");
    }

    private static string ToStringArrayExpression(ImmutableArray<string> values)
    {
        if (values.IsDefaultOrEmpty)
        {
            return "global::System.Array.Empty<string>()";
        }

        var builder = new StringBuilder();
        builder.Append("new string[] { ");
        for (var index = 0; index < values.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(ToLiteral(values[index]));
        }

        builder.Append(" }");
        return builder.ToString();
    }

    private static string GetMetadataLiteralExpression(string? expression)
    {
        if (expression is null)
        {
            return "null";
        }

        if (TryGetStringLiteralValue(expression, out var value))
        {
            return ToLiteral(value);
        }

        return "null";
    }

    private static string GetMetadataSeverityExpression(string? expression)
    {
        if (expression is null)
        {
            return ToLiteral("Error");
        }

        return expression switch
        {
            "global::StaticNorth.Valiant.Validation.ValiantSeverity.Error" => ToLiteral("Error"),
            "global::StaticNorth.Valiant.Validation.ValiantSeverity.Warning" => ToLiteral("Warning"),
            "global::StaticNorth.Valiant.Validation.ValiantSeverity.Info" => ToLiteral("Info"),
            _ => ToLiteral("(" + expression + ")")
        };
    }

    private static bool TryGetStringLiteralValue(string expression, out string value)
    {
        try
        {
            if (SyntaxFactory.ParseExpression(expression) is LiteralExpressionSyntax { Token.Value: string text })
            {
                value = text;
                return true;
            }
        }
        catch (ArgumentException)
        {
        }

        value = string.Empty;
        return false;
    }

    private static void AppendValidateMethodStart(
        StringBuilder builder,
        string? namespaceName,
        string modelType,
        bool emitAsync,
        bool awaitResult = false)
    {
        const string resultType = "global::StaticNorth.Valiant.Validation.ValidationResult";
        builder.AppendLine();
        if (emitAsync)
        {
            AppendXmlDocumentation(
                builder,
                namespaceName,
                1,
                "Asynchronously validates the specified instance.",
                "A value task containing the validation result for the specified instance.",
                ("instance", "The instance to validate."),
                ("cancellationToken", "A cancellation token used to cancel asynchronous validation."));
        }
        else
        {
            AppendXmlDocumentation(
                builder,
                namespaceName,
                1,
                "Validates the specified instance.",
                "The validation result for the specified instance.",
                ("instance", "The instance to validate."));
        }

        AppendIndent(builder, 1, namespaceName);
        if (emitAsync)
        {
            builder.Append(awaitResult ? "public async " : "public ")
                .Append("global::System.Threading.Tasks.ValueTask<").Append(resultType).Append("> ValidateAsync(")
                .Append(modelType).AppendLine(" instance, global::System.Threading.CancellationToken cancellationToken = default)");
        }
        else
        {
            builder.Append("public ").Append(resultType).Append(" Validate(").Append(modelType).AppendLine(" instance)");
        }

        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        if (emitAsync)
        {
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("cancellationToken.ThrowIfCancellationRequested();");
        }
    }

    private static void AppendGeneratedValidationMethods(
        StringBuilder builder,
        string? namespaceName,
        string modelType,
        ValidatorModel validator)
    {
        var rules = validator.Nodes.OfType<RuleNode>().Select(static node => node.Rule).ToImmutableArray();
        var totalChecks = Math.Max(rules.Sum(static rule => rule.Checks.Length), 1);
        var hasChildReuse = rules.Length != validator.Nodes.Length;
        var cacheFixedFailures = !hasChildReuse && CanCacheFixedFailures(rules, totalChecks);
        var hasPropertyReuse = rules.Any(static rule => rule.Checks[0].Kind == "UseValidator");
        var hasReuse = hasChildReuse || hasPropertyReuse;
        if (cacheFixedFailures)
        {
            AppendFixedFailureCache(builder, namespaceName, rules);
        }

        AppendValidateMethodStart(builder, namespaceName, modelType, emitAsync: false);
        if (validator.HasConfigureMethod)
        {
            AppendDebuggableDispatch(builder, namespaceName, "ConfigureValidation(instance).ToResult()");
        }

        AppendGeneratedChecks(builder, namespaceName, validator, totalChecks, cacheFixedFailures, emitAsync: false);
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        AppendValidateMethodStart(builder, namespaceName, modelType, emitAsync: true, awaitResult: hasReuse || validator.HasConfigureMethod);
        if (validator.HasConfigureMethod)
        {
            AppendDebuggableDispatch(builder, namespaceName, "await ConfigureValidation(instance).ToResultAsync(cancellationToken).ConfigureAwait(false)");
        }

        if (hasReuse)
        {
            AppendGeneratedChecks(builder, namespaceName, validator, totalChecks, cacheFixedFailures, emitAsync: true, checkCancellation: hasChildReuse);
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("}");
            if (hasPropertyReuse)
            {
                AppendCombinePropertyPathMethod(builder, namespaceName);
            }
        }
        else
        {
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine(validator.HasConfigureMethod
                ? "return ValidateWithCancellation(instance, cancellationToken);"
                : "return new global::System.Threading.Tasks.ValueTask<global::StaticNorth.Valiant.Validation.ValidationResult>(Validate(instance));");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("}");
        }

        if (validator.HasConfigureMethod && !hasReuse)
        {
            // A synchronous getter can cancel the token. Preserve the async runtime's checks
            // between rules even for validators that declare only synchronous predicates.
            builder.AppendLine();
            AppendIndent(builder, 1, namespaceName);
            builder.Append("private global::StaticNorth.Valiant.Validation.ValidationResult ValidateWithCancellation(")
                .Append(modelType).AppendLine(" instance, global::System.Threading.CancellationToken cancellationToken)");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("{");
            AppendGeneratedChecks(builder, namespaceName, validator, totalChecks, cacheFixedFailures, emitAsync: false, checkCancellation: true);
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("}");
        }
    }

    private static void AppendDebuggableDispatch(StringBuilder builder, string? namespaceName, string resultExpression)
    {
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (_valiantValidationOptions?.Mode == global::StaticNorth.Valiant.Validation.ValiantValidationMode.Debuggable)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.Append("return ").Append(resultExpression).AppendLine(";");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendGeneratedChecks(
        StringBuilder builder,
        string? namespaceName,
        ValidatorModel validator,
        int totalChecks,
        bool cacheFixedFailures,
        bool emitAsync,
        bool checkCancellation = false)
    {
        AppendRuntimeNullCheck(builder, namespaceName, validator);
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("var validationFailureBehavior = GetEffectiveFailureBehavior();");
        AppendIndent(builder, 2, namespaceName);
        if (cacheFixedFailures)
        {
            builder.AppendLine("var failures = 0;");
        }
        else
        {
            builder.Append("var failures = new global::StaticNorth.Valiant.Validation.Infrastructure.ValidationResultAccumulator(validationFailureBehavior == global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast ? 1 : ")
                .Append(totalChecks.ToString(CultureInfo.InvariantCulture))
                .AppendLine(");");
        }

        var failureIndex = cacheFixedFailures ? (int?)0 : null;
        for (var ruleIndex = 0; ruleIndex < validator.Nodes.Length; ruleIndex++)
        {
            if (validator.Nodes[ruleIndex] is RuleNode rule)
            {
                AppendGeneratedRule(builder, namespaceName, rule.Rule, ruleIndex, emitAsync, checkCancellation, failureIndex);
                failureIndex += rule.Rule.Checks.Length;
            }
            else if (validator.Nodes[ruleIndex] is ChildPropertyValidationNode child)
            {
                AppendGeneratedChildReuse(builder, namespaceName, child, ruleIndex, emitAsync);
            }
        }

        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine(cacheFixedFailures
            ? "return failures == 0 ? global::StaticNorth.Valiant.Validation.ValidationResult.Valid : __valiantFixedFailureCache.Get(failures);"
            : "return failures.Count == 0 ? global::StaticNorth.Valiant.Validation.ValidationResult.Valid : failures.Build();");
    }

    private static void AppendGeneratedChildReuse(
        StringBuilder builder, string? namespaceName, ChildPropertyValidationNode child, int ruleIndex, bool emitAsync)
    {
        var reuse = (ValidatorReuseNode)child.Nodes[0];
        var suffix = ruleIndex.ToString(CultureInfo.InvariantCulture);
        var value = "child" + suffix;
        var dependency = "childValidator" + suffix;
        var result = "childResult" + suffix;
        var prefix = reuse.PropertyPathPrefixExpression;

        builder.AppendLine();
        if (emitAsync)
        {
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("cancellationToken.ThrowIfCancellationRequested();");
        }

        AppendIndent(builder, 2, namespaceName);
        builder.Append("if (").Append(child.ChildExpression).Append(" is { } ").Append(value).AppendLine(")");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.Append("var ").Append(dependency).Append(" = this.").Append(reuse.ValidatorExpression)
            .AppendLine(" ?? throw new global::System.ArgumentNullException(\"validator\");");
        if (emitAsync)
        {
            AppendIndent(builder, 3, namespaceName);
            builder.AppendLine("cancellationToken.ThrowIfCancellationRequested();");
        }

        AppendIndent(builder, 3, namespaceName);
        builder.Append("var ").Append(result).Append(" = ").Append(emitAsync ? "await " : "").Append(dependency)
            .Append(emitAsync ? ".ValidateAsync(" : ".Validate(").Append(value)
            .AppendLine(emitAsync ? ", cancellationToken).ConfigureAwait(false);" : ");");
        AppendIndent(builder, 3, namespaceName);
        builder.Append("foreach (var failure in ").Append(result).AppendLine(".Failures)");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 4, namespaceName);
        builder.Append("var path = global::System.String.IsNullOrEmpty(failure.PropertyName) ? ").Append(prefix)
            .Append(".TrimEnd('.') : failure.PropertyName.StartsWith(").Append(prefix)
            .Append(", global::System.StringComparison.Ordinal) ? failure.PropertyName : ").Append(prefix)
            .AppendLine(" + failure.PropertyName;");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("failures.Add(new global::StaticNorth.Valiant.Validation.ValidationFailure(path, failure.ErrorCode, failure.ErrorMessage, failure.Severity));");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("if (validationFailureBehavior == global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast && failure.Severity == global::StaticNorth.Valiant.Validation.ValiantSeverity.Error)");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 5, namespaceName);
        builder.AppendLine("return failures.Build();");
        AppendIndent(builder, 4, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendCombinePropertyPathMethod(StringBuilder builder, string? namespaceName)
    {
        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private static string CombinePropertyPath(string propertyName, string childPropertyName)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (global::System.String.IsNullOrEmpty(childPropertyName))");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("return propertyName;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return childPropertyName.StartsWith(\"[\", global::System.StringComparison.Ordinal)");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("? propertyName + childPropertyName");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine(": propertyName + \".\" + childPropertyName;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

    private static DependencyInjectionValidatorModel CreateDependencyInjectionValidatorModel(ValidatorModel validator)
    {
        return new DependencyInjectionValidatorModel(
            validator.Declaration.Type,
            validator.ModelType!,
            validator.Declaration.MetadataProviderType,
            validator.RegisterAsDefault);
    }

    private static string GenerateDependencyInjection(
        ImmutableArray<DependencyInjectionValidatorModel> validators,
        ImmutableArray<DependencyInjectionPropertyValidatorModel> propertyValidators)
    {
        var builder = new StringBuilder();
        AppendGeneratedFileHeader(builder);
        builder.AppendLine("namespace StaticNorth.Valiant.Validation.DependencyInjection");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Provides dependency injection registration helpers for Valiant validators discovered by the source generator.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    public static class ValiantServiceCollectionExtensions");
        builder.AppendLine("    {");
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Registers all Valiant validators discovered in the current compilation.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        /// <param name=\"services\">The service collection to add validators to.</param>");
        builder.AppendLine("        /// <param name=\"configure\">An optional callback used to configure Valiant validation options.</param>");
        builder.AppendLine("        /// <returns>The same service collection instance so calls can be chained.</returns>");
        builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddValiantValidators(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, global::System.Action<global::StaticNorth.Valiant.Validation.ValiantValidationOptions>? configure = null)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (services is null)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::System.ArgumentNullException(nameof(services));");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            var valiantValidationOptions = new global::StaticNorth.Valiant.Validation.ValiantValidationOptions();");
        builder.AppendLine("            configure?.Invoke(valiantValidationOptions);");
        builder.AppendLine("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAdd(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(global::StaticNorth.Valiant.Validation.ValiantValidationOptions), valiantValidationOptions));");
        builder.AppendLine();

        foreach (var validator in validators.OrderBy(static validator => validator.ValidatorType, StringComparer.Ordinal))
        {
            builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAdd(services, CreateValidatorDescriptor<")
                .Append(validator.ValidatorType)
                .Append(", ")
                .Append(validator.ValidatorType)
                .AppendLine(">(valiantValidationOptions.Lifetime));");

            if (validator.RegisterAsDefault)
            {
                builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, CreateValidatorDescriptor<global::StaticNorth.Valiant.Validation.IValidator<")
                    .Append(validator.ModelType)
                    .Append(">, ")
                    .Append(validator.ValidatorType)
                    .AppendLine(">(valiantValidationOptions.Lifetime));");

                builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::StaticNorth.Valiant.Validation.IValiantValidationMetadataProvider, ")
                    .Append(validator.MetadataProviderType)
                    .AppendLine(">());");
            }
        }

        foreach (var modelType in validators
            .Where(static validator => validator.RegisterAsDefault)
            .Select(static validator => validator.ModelType)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static modelType => modelType, StringComparer.Ordinal))
        {
            builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::StaticNorth.Valiant.Validation.Infrastructure.IValiantValidatorInvoker, global::StaticNorth.Valiant.Validation.Infrastructure.ValiantValidatorInvoker<")
                .Append(modelType)
                .AppendLine(">>());");
        }

        foreach (var propertyValidator in propertyValidators.OrderBy(static validator => validator.ValidatorType, StringComparer.Ordinal))
        {
            builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAdd(services, CreateValidatorDescriptor<")
                .Append(propertyValidator.ValidatorType)
                .Append(", ")
                .Append(propertyValidator.ValidatorType)
                .AppendLine(">(valiantValidationOptions.Lifetime));");

            foreach (var propertyType in propertyValidator.PropertyTypes)
            {
                builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, CreateValidatorDescriptor<global::StaticNorth.Valiant.Validation.IPropertyValidator<")
                    .Append(propertyType)
                    .Append(">, ")
                    .Append(propertyValidator.ValidatorType)
                    .AppendLine(">(valiantValidationOptions.Lifetime));");
            }
        }

        builder.AppendLine();
        builder.AppendLine("            return services;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        private static global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor CreateValidatorDescriptor<TService, [global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(global::StaticNorth.Valiant.Validation.ValiantValidatorLifetime lifetime)");
        builder.AppendLine("            where TService : class");
        builder.AppendLine("            where TImplementation : class, TService");
        builder.AppendLine("        {");
        builder.AppendLine("            switch (lifetime)");
        builder.AppendLine("            {");
        builder.AppendLine("                case global::StaticNorth.Valiant.Validation.ValiantValidatorLifetime.Scoped:");
        builder.AppendLine("                    return global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Scoped<TService, TImplementation>();");
        builder.AppendLine("                case global::StaticNorth.Valiant.Validation.ValiantValidatorLifetime.Transient:");
        builder.AppendLine("                    return global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Transient<TService, TImplementation>();");
        builder.AppendLine("                default:");
        builder.AppendLine("                    return global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<TService, TImplementation>();");
        builder.AppendLine("            }");
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
                case ValidatorReuseNode:
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
                case LoopNode loopNode:
                    if (HasAsyncChecks(loopNode.Nodes))
                    {
                        return true;
                    }

                    break;
                case CollectionIterationNode iterationNode:
                    if (HasAsyncChecks(iterationNode.Nodes))
                    {
                        return true;
                    }

                    break;
                case ChildPropertyValidationNode childNode:
                    if (HasAsyncChecks(childNode.Nodes))
                    {
                        return true;
                    }

                    break;
                case FailureBehaviorNode:
                    break;
                case RawStatementNode:
                    break;
            }
        }

        return false;
    }

    private static bool CanCacheFixedFailures(ImmutableArray<RuleModel> rules, int totalChecks)
    {
        // Bound retained results to 63 combinations. Composition and dynamic failure content
        // keep the owned accumulator path; no validation decisions or model values are cached.
        if (rules.IsDefaultOrEmpty || totalChecks > 6)
        {
            return false;
        }

        foreach (var rule in rules)
        {
            foreach (var check in rule.Checks)
            {
                if (check.Kind == "UseValidator" || check.Code is not null || check.Message is not null || check.Severity is not null ||
                    !TryGetStringLiteralValue(GetDefaultMessageExpression(rule, check), out _))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void AppendFixedFailureCache(
        StringBuilder builder,
        string? namespaceName,
        ImmutableArray<RuleModel> rules)
    {
        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private static readonly global::StaticNorth.Valiant.Validation.Infrastructure.ValidationResultCache __valiantFixedFailureCache = new(new global::StaticNorth.Valiant.Validation.ValidationFailure[]");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        foreach (var rule in rules)
        {
            foreach (var check in rule.Checks)
            {
                AppendIndent(builder, 2, namespaceName);
                builder.Append("new global::StaticNorth.Valiant.Validation.ValidationFailure(").Append(rule.PropertyNameExpression).Append(", ")
                    .Append(GetFailureCodeExpression(check)).Append(", ")
                    .Append(GetFailureMessageExpression(rule, check)).AppendLine("),");
            }
        }

        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("});");
    }

    private static void AppendGeneratedRule(
        StringBuilder builder,
        string? namespaceName,
        RuleModel rule,
        int ruleIndex,
        bool emitAsync,
        bool checkCancellation,
        int? failureIndex)
    {
        var localName = "value" + ruleIndex.ToString(CultureInfo.InvariantCulture);
        var indentLevel = 2;

        builder.AppendLine();
        if (emitAsync || checkCancellation)
        {
            AppendIndent(builder, indentLevel, namespaceName);
            builder.AppendLine("cancellationToken.ThrowIfCancellationRequested();");
        }

        if (rule.ConditionExpression is not null)
        {
            AppendIndent(builder, indentLevel, namespaceName);
            builder.Append("if (").Append(GetRuleConditionInvocation(rule)).AppendLine(")");
            AppendIndent(builder, indentLevel, namespaceName);
            builder.AppendLine("{");
            indentLevel++;
        }

        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("var ").Append(localName).Append(" = ").Append(rule.InstanceExpression).Append('.').Append(rule.PropertyAccess).AppendLine(";");

        for (var index = 0; index < rule.Checks.Length; index++)
        {
            if (checkCancellation && index > 0)
            {
                AppendIndent(builder, indentLevel, namespaceName);
                builder.AppendLine("cancellationToken.ThrowIfCancellationRequested();");
            }

            AppendGeneratedCheck(
                builder, namespaceName, indentLevel, localName, rule, rule.Checks[index], emitAsync, failureIndex + index);
        }

        if (rule.ConditionExpression is not null)
        {
            AppendIndent(builder, indentLevel - 1, namespaceName);
            builder.AppendLine("}");
        }
    }

    private static string GetRuleConditionInvocation(RuleModel rule)
    {
        if (rule.ConditionExpression is null || rule.ConditionDelegateType is null)
        {
            throw new InvalidOperationException("Conditional rule is missing condition metadata.");
        }

        return "((" + rule.ConditionDelegateType + ")(" + rule.ConditionExpression + "))(" + rule.InstanceExpression + ")";
    }

    private static void AppendGeneratedCheck(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        string localName,
        RuleModel rule,
        RuleCheck check,
        bool emitAsync,
        int? cachedFailureIndex)
    {
        if (check.Kind == "UseValidator")
        {
            AppendAttributePropertyValidatorReuse(builder, namespaceName, indentLevel, localName, rule, check, emitAsync);
            return;
        }

        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("if (").Append(GetFailureCondition(localName, rule, check)).AppendLine(")");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, indentLevel + 1, namespaceName);
        if (cachedFailureIndex is not null)
        {
            builder.Append("failures |= ").Append((1 << cachedFailureIndex.Value).ToString(CultureInfo.InvariantCulture)).AppendLine(";");
        }
        else
        {
            builder.Append("failures.Add(new global::StaticNorth.Valiant.Validation.ValidationFailure(")
                .Append(rule.PropertyNameExpression)
                .Append(", ")
                .Append(GetFailureCodeExpression(check))
                .Append(", ")
                .Append(GetFailureMessageExpression(rule, check))
                .Append(", ")
                .Append(GetFailureSeverityExpression(check))
                .AppendLine("));");
        }
        AppendIndent(builder, indentLevel + 1, namespaceName);
        if (check.Severity is null)
        {
            builder.AppendLine("if (validationFailureBehavior == global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast)");
        }
        else
        {
            builder.Append("if (validationFailureBehavior == global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast && ")
                .Append(GetFailureSeverityExpression(check))
                .AppendLine(" == global::StaticNorth.Valiant.Validation.ValiantSeverity.Error)");
        }
        AppendIndent(builder, indentLevel + 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, indentLevel + 2, namespaceName);
        builder.AppendLine(cachedFailureIndex is not null
            ? "return __valiantFixedFailureCache.Get(failures);"
            : "return failures.Build();");
        AppendIndent(builder, indentLevel + 1, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendAttributePropertyValidatorReuse(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        string localName,
        RuleModel rule,
        RuleCheck check,
        bool emitAsync)
    {
        var resultName = "propertyValidatorResult" + localName.Substring("value".Length);
        var failureName = "propertyValidatorFailure" + localName.Substring("value".Length);

        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("if (").Append(localName).AppendLine(" is not null)");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, indentLevel + 1, namespaceName);
        if (emitAsync)
        {
            builder.Append("var ").Append(resultName).Append(" = await ").Append(check.Arguments[0])
                .Append(".ValidateAsync(").Append(localName).AppendLine(", cancellationToken).ConfigureAwait(false);");
        }
        else
        {
            builder.Append("var ").Append(resultName).Append(" = ").Append(check.Arguments[0])
                .Append(".Validate(").Append(localName).AppendLine(");");
        }

        AppendIndent(builder, indentLevel + 1, namespaceName);
        builder.Append("if (").Append(resultName).AppendLine(".Failures.Count > 0)");
        AppendIndent(builder, indentLevel + 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, indentLevel + 2, namespaceName);
        builder.Append("foreach (var ").Append(failureName).Append(" in ").Append(resultName).AppendLine(".Failures)");
        AppendIndent(builder, indentLevel + 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, indentLevel + 3, namespaceName);
        builder.Append("failures.Add(new global::StaticNorth.Valiant.Validation.ValidationFailure(CombinePropertyPath(")
            .Append(rule.PropertyNameExpression)
            .Append(", ")
            .Append(failureName)
            .Append(".PropertyName), ")
            .Append(failureName)
            .Append(".ErrorCode, ")
            .Append(failureName)
            .Append(".ErrorMessage, ")
            .Append(failureName)
            .AppendLine(".Severity));");
        AppendIndent(builder, indentLevel + 3, namespaceName);
        builder.AppendLine("if (validationFailureBehavior == global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast && " + failureName + ".Severity == global::StaticNorth.Valiant.Validation.ValiantSeverity.Error)");
        AppendIndent(builder, indentLevel + 3, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, indentLevel + 4, namespaceName);
        builder.AppendLine("return failures.Build();");
        AppendIndent(builder, indentLevel + 3, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, indentLevel + 2, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, indentLevel + 1, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("}");
    }

    private static string GetFailureCodeExpression(RuleCheck check)
    {
        return check.Code is null
            ? ToLiteral(check.Kind)
            : "(" + check.Code + " ?? " + ToLiteral(check.Kind) + ")";
    }

    private static string GetFailureMessageExpression(RuleModel rule, RuleCheck check)
    {
        var defaultMessage = GetDefaultMessageExpression(rule, check);
        return check.Message is null
            ? defaultMessage
            : "(" + check.Message + " ?? " + defaultMessage + ")";
    }

    private static string GetFailureSeverityExpression(RuleCheck check)
    {
        return check.Severity ?? "global::StaticNorth.Valiant.Validation.ValiantSeverity.Error";
    }

    private static string GetFailureCondition(string localName, RuleModel rule, RuleCheck check)
    {
        switch (check.Kind)
        {
            case "NotNull":
                return localName + " is null";
            case "Null":
                return localName + " is not null";
            case "Required":
                if (rule.IsString)
                {
                    return "global::System.String.IsNullOrWhiteSpace(" + localName + ")";
                }

                return "global::StaticNorth.Valiant.Validation.ValidationChecks.IsRequiredValueMissing(" + localName + ")";
            case "NotEmpty":
                return "global::StaticNorth.Valiant.Validation.ValidationChecks.IsEmpty(" + localName + ")";
            case "Empty":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.IsEmpty(" + localName + ")";
            case "DefinedEnum":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.IsDefinedEnum(" + localName + ")";
            case "EnumName":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.IsEnumName(" + check.Arguments[0] + ", " + localName + ")";
            case "EnumValue":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.IsEnumValue(" + check.Arguments[0] + ", " + localName + ")";
            case "Count":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.HasCount(" + localName + ", " + check.Arguments[0] + ")";
            case "CountBetween":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.HasCountBetween(" + localName + ", " + check.Arguments[0] + ", " + check.Arguments[1] + ")";
            case "MinCount":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.HasMinCount(" + localName + ", " + check.Arguments[0] + ")";
            case "MaxCount":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.HasMaxCount(" + localName + ", " + check.Arguments[0] + ")";
            case "Contains":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.Contains(" + localName + ", " + check.Arguments[0] + ")";
            case "Excludes":
                return "global::StaticNorth.Valiant.Validation.ValidationChecks.Contains(" + localName + ", " + check.Arguments[0] + ")";
            case "HasKey":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.HasKey(" + localName + ", " + check.Arguments[0] + ")";
            case "HasValue":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.HasValue(" + localName + ", " + check.Arguments[0] + ")";
            case "LengthBetween":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.HasLengthBetween(" + localName + ", " + check.Arguments[0] + ", " + check.Arguments[1] + ")";
            case "MinLength":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.HasMinLength(" + localName + ", " + check.Arguments[0] + ")";
            case "MaxLength":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.HasMaxLength(" + localName + ", " + check.Arguments[0] + ")";
            case "GreaterThan":
                return GetComparisonFailureCondition(rule, localName, check.Arguments[0], "<= 0");
            case "GreaterThanOrEqualTo":
                return GetComparisonFailureCondition(rule, localName, check.Arguments[0], "< 0");
            case "LessThan":
                return GetComparisonFailureCondition(rule, localName, check.Arguments[0], ">= 0");
            case "LessThanOrEqualTo":
                return GetComparisonFailureCondition(rule, localName, check.Arguments[0], "> 0");
            case "Equal":
                return "!global::System.Collections.Generic.EqualityComparer<" + rule.PropertyType + ">.Default.Equals(" + localName + ", " + check.Arguments[0] + ")";
            case "NotEqual":
                return "global::System.Collections.Generic.EqualityComparer<" + rule.PropertyType + ">.Default.Equals(" + localName + ", " + check.Arguments[0] + ")";
            case "InclusiveBetween":
                return GetBetweenFailureCondition(rule, localName, check.Arguments[0], "< 0", check.Arguments[1], "> 0");
            case "ExclusiveBetween":
                return GetBetweenFailureCondition(rule, localName, check.Arguments[0], "<= 0", check.Arguments[1], ">= 0");
            default:
                throw new InvalidOperationException("Unsupported check kind '" + check.Kind + "'.");
        }
    }

    private static string GetComparisonFailureCondition(RuleModel rule, string localName, string argument, string comparison)
    {
        var condition = "global::System.Collections.Generic.Comparer<" + rule.PropertyType + ">.Default.Compare(" + localName + ", " + argument + ") " + comparison;
        return rule.IsNullableValueType
            ? localName + ".HasValue && " + condition
            : condition;
    }

    private static string GetBetweenFailureCondition(
        RuleModel rule,
        string localName,
        string lowerArgument,
        string lowerComparison,
        string upperArgument,
        string upperComparison)
    {
        var lowerCondition = "global::System.Collections.Generic.Comparer<" + rule.PropertyType + ">.Default.Compare(" + localName + ", " + lowerArgument + ") " + lowerComparison;
        var upperCondition = "global::System.Collections.Generic.Comparer<" + rule.PropertyType + ">.Default.Compare(" + localName + ", " + upperArgument + ") " + upperComparison;
        var condition = lowerCondition + " || " + upperCondition;
        return rule.IsNullableValueType
            ? localName + ".HasValue && (" + condition + ")"
            : condition;
    }

    private static string GetDefaultMessageExpression(RuleModel rule, RuleCheck check)
    {
        if (rule.PropertyNameExpression == ToLiteral(rule.PropertyName) &&
            (!RequiresRuntimeArgumentFormatting(check.Kind) ||
                HasCultureInvariantIntegerLiteralArguments(check)))
        {
            return ToLiteral(GetDefaultMessageText(rule, check));
        }

        var propertyName = "\"'\" + " + rule.PropertyNameExpression + " + \"'\"";
        switch (check.Kind)
        {
            case "NotNull":
                return propertyName + " + \" must not be null.\"";
            case "Null":
                return propertyName + " + \" must be null.\"";
            case "Required":
                return propertyName + " + \" is required.\"";
            case "NotEmpty":
                return propertyName + " + \" must not be empty.\"";
            case "Empty":
                return propertyName + " + \" must be empty.\"";
            case "DefinedEnum":
                return propertyName + " + \" must be a defined enum value.\"";
            case "EnumName":
                return propertyName + " + \" must be a valid enum name.\"";
            case "EnumValue":
                return propertyName + " + \" must be a valid enum value.\"";
            case "Count":
                return propertyName + " + " + ToLiteral(" must contain exactly " + check.Arguments[0] + " item(s).");
            case "CountBetween":
                return propertyName + " + " + ToLiteral(" must contain between " + check.Arguments[0] + " and " + check.Arguments[1] + " item(s).");
            case "MinCount":
                return propertyName + " + " + ToLiteral(" must contain at least " + check.Arguments[0] + " item(s).");
            case "MaxCount":
                return propertyName + " + " + ToLiteral(" must contain at most " + check.Arguments[0] + " item(s).");
            case "Contains":
                return propertyName + " + \" must contain the expected item.\"";
            case "Excludes":
                return propertyName + " + \" must not contain the excluded item.\"";
            case "HasKey":
                return propertyName + " + \" must contain the expected key.\"";
            case "HasValue":
                return propertyName + " + \" must contain the expected value.\"";
            case "All":
                return propertyName + " + \" must contain only matching item(s).\"";
            case "Any":
                return propertyName + " + \" must contain at least one matching item.\"";
            case "None":
                return propertyName + " + \" must not contain matching item(s).\"";
            case "LengthBetween":
                return propertyName + " + " + ToLiteral(rule.IsString
                    ? " must be between " + check.Arguments[0] + " and " + check.Arguments[1] + " characters."
                    : " must contain between " + check.Arguments[0] + " and " + check.Arguments[1] + " item(s).");
            case "MinLength":
                return propertyName + " + " + ToLiteral(rule.IsString
                    ? " must be at least " + check.Arguments[0] + " characters."
                    : " must contain at least " + check.Arguments[0] + " item(s).");
            case "MaxLength":
                return propertyName + " + " + ToLiteral(rule.IsString
                    ? " must be at most " + check.Arguments[0] + " characters."
                    : " must contain at most " + check.Arguments[0] + " item(s).");
            case "GreaterThan":
                return propertyName + " + \" must be greater than \" + (" + check.Arguments[0] + ") + \".\"";
            case "GreaterThanOrEqualTo":
                return propertyName + " + \" must be greater than or equal to \" + (" + check.Arguments[0] + ") + \".\"";
            case "LessThan":
                return propertyName + " + \" must be less than \" + (" + check.Arguments[0] + ") + \".\"";
            case "LessThanOrEqualTo":
                return propertyName + " + \" must be less than or equal to \" + (" + check.Arguments[0] + ") + \".\"";
            case "Equal":
                return propertyName + " + \" must be equal to \" + (" + check.Arguments[0] + ") + \".\"";
            case "NotEqual":
                return propertyName + " + \" must not be equal to \" + (" + check.Arguments[0] + ") + \".\"";
            case "InclusiveBetween":
                return propertyName + " + \" must be between \" + (" + check.Arguments[0] + ") + \" and \" + (" + check.Arguments[1] + ") + \".\"";
            case "ExclusiveBetween":
                return propertyName + " + \" must be between \" + (" + check.Arguments[0] + ") + \" and \" + (" + check.Arguments[1] + ") + \", exclusive.\"";
            case "Satisfies":
            case "SatisfiesAsync":
                return propertyName + " + \" did not satisfy the custom validation predicate.\"";
            default:
                throw new InvalidOperationException("Unsupported check kind '" + check.Kind + "'.");
        }
    }

    private static bool HasCultureInvariantIntegerLiteralArguments(RuleCheck check)
    {
        if (check.Arguments.IsEmpty)
        {
            return false;
        }

        foreach (var argument in check.Arguments)
        {
            if (!IsCanonicalNonNegativeIntegerLiteral(argument))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCanonicalNonNegativeIntegerLiteral(string argument)
    {
        if (argument.Length == 0 || (argument.Length > 1 && argument[0] == '0'))
        {
            return false;
        }

        foreach (var character in argument)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static bool RequiresRuntimeArgumentFormatting(string checkKind)
    {
        switch (checkKind)
        {
            case "GreaterThan":
            case "GreaterThanOrEqualTo":
            case "LessThan":
            case "LessThanOrEqualTo":
            case "Equal":
            case "NotEqual":
            case "InclusiveBetween":
            case "ExclusiveBetween":
                return true;
            default:
                return false;
        }
    }

    private static string GetDefaultMessageText(RuleModel rule, RuleCheck check)
    {
        switch (check.Kind)
        {
            case "NotNull":
                return "'" + rule.PropertyName + "' must not be null.";
            case "Null":
                return "'" + rule.PropertyName + "' must be null.";
            case "Required":
                return "'" + rule.PropertyName + "' is required.";
            case "NotEmpty":
                return "'" + rule.PropertyName + "' must not be empty.";
            case "Empty":
                return "'" + rule.PropertyName + "' must be empty.";
            case "DefinedEnum":
                return "'" + rule.PropertyName + "' must be a defined enum value.";
            case "EnumName":
                return "'" + rule.PropertyName + "' must be a valid enum name.";
            case "EnumValue":
                return "'" + rule.PropertyName + "' must be a valid enum value.";
            case "Count":
                return "'" + rule.PropertyName + "' must contain exactly " + check.Arguments[0] + " item(s).";
            case "CountBetween":
                return "'" + rule.PropertyName + "' must contain between " + check.Arguments[0] + " and " + check.Arguments[1] + " item(s).";
            case "MinCount":
                return "'" + rule.PropertyName + "' must contain at least " + check.Arguments[0] + " item(s).";
            case "MaxCount":
                return "'" + rule.PropertyName + "' must contain at most " + check.Arguments[0] + " item(s).";
            case "Contains":
                return "'" + rule.PropertyName + "' must contain the expected item.";
            case "Excludes":
                return "'" + rule.PropertyName + "' must not contain the excluded item.";
            case "HasKey":
                return "'" + rule.PropertyName + "' must contain the expected key.";
            case "HasValue":
                return "'" + rule.PropertyName + "' must contain the expected value.";
            case "All":
                return "'" + rule.PropertyName + "' must contain only matching item(s).";
            case "Any":
                return "'" + rule.PropertyName + "' must contain at least one matching item.";
            case "None":
                return "'" + rule.PropertyName + "' must not contain matching item(s).";
            case "LengthBetween":
                return rule.IsString
                    ? "'" + rule.PropertyName + "' must be between " + check.Arguments[0] + " and " + check.Arguments[1] + " characters."
                    : "'" + rule.PropertyName + "' must contain between " + check.Arguments[0] + " and " + check.Arguments[1] + " item(s).";
            case "MinLength":
                return rule.IsString
                    ? "'" + rule.PropertyName + "' must be at least " + check.Arguments[0] + " characters."
                    : "'" + rule.PropertyName + "' must contain at least " + check.Arguments[0] + " item(s).";
            case "MaxLength":
                return rule.IsString
                    ? "'" + rule.PropertyName + "' must be at most " + check.Arguments[0] + " characters."
                    : "'" + rule.PropertyName + "' must contain at most " + check.Arguments[0] + " item(s).";
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
            case "Satisfies":
            case "SatisfiesAsync":
                return "'" + rule.PropertyName + "' did not satisfy the custom validation predicate.";
            default:
                throw new InvalidOperationException("Unsupported check kind '" + check.Kind + "'.");
        }
    }
}
