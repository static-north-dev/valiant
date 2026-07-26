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
        var hasAsyncChecks = HasAsyncChecks(validator.Nodes);
        var requiresAsyncValidationBody = hasAsyncChecks || HasValidatorReuse(validator.Nodes);
        var totalChecks = Math.Max(CountChecks(validator.Nodes), 1);

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

        AppendXmlDocumentation(
            builder,
            namespaceName,
            1,
            "Validates the specified instance.",
            "The validation result for the specified instance.",
            ("instance", "The instance to validate."));
        AppendIndent(builder, 1, namespaceName);
        builder.Append(GetGeneratedValidatorMethodModifier(validator))
            .Append("global::StaticNorth.Valiant.Validation.ValidationResult Validate(")
            .Append(modelType)
            .AppendLine(" instance)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        if (validator.HasConfigureMethod)
        {
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("if (GetEffectiveValidationMode() == global::StaticNorth.Valiant.Validation.ValiantValidationMode.Debuggable)");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, 3, namespaceName);
            builder.AppendLine("return ValidateRuntime(instance);");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("}");
            builder.AppendLine();
        }

        if (hasAsyncChecks)
        {
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("throw new global::System.InvalidOperationException(\"This validator contains asynchronous validations. Call ValidateAsync instead.\");");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("}");

            AppendCommonMembers(builder, namespaceName, modelType, validator, totalChecks, requiresAsyncValidationBody);
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

        if (!validator.IsModelValueType)
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
        builder.AppendLine("global::System.Collections.Generic.List<global::StaticNorth.Valiant.Validation.ValidationFailure>? failures = null;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("var validationFailureBehavior = GetEffectiveFailureBehavior();");

        var ruleIndex = 0;
        AppendValidationNodes(builder, namespaceName, 2, validator.Nodes, ref ruleIndex, totalChecks, false);

        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return failures is null ? global::StaticNorth.Valiant.Validation.ValidationResult.Valid : new global::StaticNorth.Valiant.Validation.ValidationResult(failures);");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
        AppendCommonMembers(builder, namespaceName, modelType, validator, totalChecks, requiresAsyncValidationBody);
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

    private static string GetGeneratedValidatorMethodModifier(ValidatorModel validator)
    {
        return "public ";
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

    private static void AppendCommonMembers(
        StringBuilder builder,
        string? namespaceName,
        string modelType,
        ValidatorModel validator,
        int totalChecks,
        bool requiresAsyncValidationBody)
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
        builder.Append(GetGeneratedValidatorMethodModifier(validator))
            .Append("bool IsValid(")
            .Append(modelType)
            .AppendLine(" instance)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return Validate(instance).IsValid;");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

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

        builder.AppendLine();
        AppendEffectiveFailureBehaviorMember(builder, namespaceName);

        builder.AppendLine();
        if (validator.HasConfigureMethod)
        {
            AppendRuntimeValidationMembers(builder, namespaceName, modelType, validator);
        }

        builder.AppendLine();
        if (requiresAsyncValidationBody)
        {
            AppendAsyncValidationMembers(builder, namespaceName, modelType, validator, totalChecks);
        }
        else
        {
            AppendXmlDocumentation(
                builder,
                namespaceName,
                1,
                "Asynchronously validates the specified instance.",
                "A value task containing the validation result for the specified instance.",
                ("instance", "The instance to validate."),
                ("cancellationToken", "A cancellation token used to cancel asynchronous validation."));
            AppendIndent(builder, 1, namespaceName);
            builder.Append(GetGeneratedValidatorMethodModifier(validator))
                .Append("global::System.Threading.Tasks.ValueTask<global::StaticNorth.Valiant.Validation.ValidationResult> ValidateAsync(")
                .Append(modelType)
                .AppendLine(" instance, global::System.Threading.CancellationToken cancellationToken = default)");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("{");
            if (validator.HasConfigureMethod)
            {
                AppendIndent(builder, 2, namespaceName);
                builder.AppendLine("if (GetEffectiveValidationMode() == global::StaticNorth.Valiant.Validation.ValiantValidationMode.Debuggable)");
                AppendIndent(builder, 2, namespaceName);
                builder.AppendLine("{");
                AppendIndent(builder, 3, namespaceName);
                builder.AppendLine("return ValidateRuntimeAsync(instance, cancellationToken);");
                AppendIndent(builder, 2, namespaceName);
                builder.AppendLine("}");
                builder.AppendLine();
            }

            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("_ = cancellationToken;");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("return new global::System.Threading.Tasks.ValueTask<global::StaticNorth.Valiant.Validation.ValidationResult>(Validate(instance));");
            AppendIndent(builder, 1, namespaceName);
            builder.AppendLine("}");

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
            builder.Append(GetGeneratedValidatorMethodModifier(validator))
                .Append("global::System.Threading.Tasks.ValueTask<bool> IsValidAsync(")
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

    private static void AppendRuntimeValidationMembers(
        StringBuilder builder,
        string? namespaceName,
        string modelType,
        ValidatorModel validator)
    {
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("private global::StaticNorth.Valiant.Validation.ValiantValidationMode GetEffectiveValidationMode()");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("var mode = _valiantValidationOptions?.Mode ?? global::StaticNorth.Valiant.Validation.ValiantValidationMode.Auto;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (mode != global::StaticNorth.Valiant.Validation.ValiantValidationMode.Auto)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("return mode;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        builder.AppendLine("#if DEBUG");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return global::StaticNorth.Valiant.Validation.ValiantValidationMode.Debuggable;");
        builder.AppendLine("#else");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return global::StaticNorth.Valiant.Validation.ValiantValidationMode.Fast;");
        builder.AppendLine("#endif");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.Append("private global::StaticNorth.Valiant.Validation.ValidationResult ValidateRuntime(").Append(modelType).AppendLine(" instance)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendRuntimeNullCheck(builder, namespaceName, validator);
        AppendIndent(builder, 2, namespaceName);
        builder.Append("var context = new global::StaticNorth.Valiant.Validation.ValidationContext<").Append(modelType).AppendLine(">(instance);");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("context.ValidateAll();");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (GetEffectiveFailureBehavior() == global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("context.FailFast();");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, 2, namespaceName);
        AppendConfigureInvocation(builder, modelType, namespaceName, validator);
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return context.ToResult();");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

        builder.AppendLine();
        AppendIndent(builder, 1, namespaceName);
        builder.Append("private async global::System.Threading.Tasks.ValueTask<global::StaticNorth.Valiant.Validation.ValidationResult> ValidateRuntimeAsync(")
            .Append(modelType)
            .AppendLine(" instance, global::System.Threading.CancellationToken cancellationToken = default)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("cancellationToken.ThrowIfCancellationRequested();");
        AppendRuntimeNullCheck(builder, namespaceName, validator);
        AppendIndent(builder, 2, namespaceName);
        builder.Append("var context = new global::StaticNorth.Valiant.Validation.ValidationContext<").Append(modelType).AppendLine(">(instance);");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("context.ValidateAll();");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("if (GetEffectiveFailureBehavior() == global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast)");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, 3, namespaceName);
        builder.AppendLine("context.FailFast();");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, 2, namespaceName);
        AppendConfigureInvocation(builder, modelType, namespaceName, validator);
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return await context.ToResultAsync(cancellationToken).ConfigureAwait(false);");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendConfigureInvocation(
        StringBuilder builder,
        string modelType,
        string? namespaceName,
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

        if (TryGetStringLiteralValue(expression, out var literalValue))
        {
            return ToLiteral(literalValue);
        }

        var lastDot = expression.LastIndexOf('.');
        return ToLiteral(lastDot >= 0 ? expression.Substring(lastDot + 1) : expression);
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

    private static void AppendAsyncValidationMembers(
        StringBuilder builder,
        string? namespaceName,
        string modelType,
        ValidatorModel validator,
        int totalChecks)
    {
        AppendXmlDocumentation(
            builder,
            namespaceName,
            1,
            "Asynchronously validates the specified instance.",
            "A value task containing the validation result for the specified instance.",
            ("instance", "The instance to validate."),
            ("cancellationToken", "A cancellation token used to cancel asynchronous validation."));
        AppendIndent(builder, 1, namespaceName);
        builder.Append(GetGeneratedValidatorMethodModifier(validator))
            .Append("async global::System.Threading.Tasks.ValueTask<global::StaticNorth.Valiant.Validation.ValidationResult> ValidateAsync(")
            .Append(modelType)
            .AppendLine(" instance, global::System.Threading.CancellationToken cancellationToken = default)");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("{");
        if (validator.HasConfigureMethod)
        {
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("if (GetEffectiveValidationMode() == global::StaticNorth.Valiant.Validation.ValiantValidationMode.Debuggable)");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, 3, namespaceName);
            builder.AppendLine("return await ValidateRuntimeAsync(instance, cancellationToken).ConfigureAwait(false);");
            AppendIndent(builder, 2, namespaceName);
            builder.AppendLine("}");
            builder.AppendLine();
        }

        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("cancellationToken.ThrowIfCancellationRequested();");

        if (!validator.IsModelValueType)
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
        builder.AppendLine("global::System.Collections.Generic.List<global::StaticNorth.Valiant.Validation.ValidationFailure>? failures = null;");
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("var validationFailureBehavior = GetEffectiveFailureBehavior();");

        var ruleIndex = 0;
        AppendValidationNodes(builder, namespaceName, 2, validator.Nodes, ref ruleIndex, totalChecks, true);

        builder.AppendLine();
        AppendIndent(builder, 2, namespaceName);
        builder.AppendLine("return failures is null ? global::StaticNorth.Valiant.Validation.ValidationResult.Valid : new global::StaticNorth.Valiant.Validation.ValidationResult(failures);");
        AppendIndent(builder, 1, namespaceName);
        builder.AppendLine("}");

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
        builder.Append(GetGeneratedValidatorMethodModifier(validator))
            .Append("async global::System.Threading.Tasks.ValueTask<bool> IsValidAsync(")
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

        foreach (var validator in validators)
        {
            builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAdd(services, CreateValidatorDescriptor(typeof(")
                .Append(validator.ValidatorType)
                .Append("), typeof(")
                .Append(validator.ValidatorType)
                .AppendLine("), valiantValidationOptions.Lifetime));");

            if (validator.RegisterAsDefault)
            {
                builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, CreateValidatorDescriptor(typeof(global::StaticNorth.Valiant.Validation.IValidator<")
                    .Append(validator.ModelType)
                    .Append(">), typeof(")
                    .Append(validator.ValidatorType)
                    .AppendLine("), valiantValidationOptions.Lifetime));");

                builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::StaticNorth.Valiant.Validation.IValiantValidationMetadataProvider, ")
                    .Append(validator.MetadataProviderType)
                    .AppendLine(">());");
            }
        }

        foreach (var propertyValidator in propertyValidators)
        {
            builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAdd(services, CreateValidatorDescriptor(typeof(")
                .Append(propertyValidator.ValidatorType)
                .Append("), typeof(")
                .Append(propertyValidator.ValidatorType)
                .AppendLine("), valiantValidationOptions.Lifetime));");

            foreach (var propertyType in propertyValidator.PropertyTypes)
            {
                builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, CreateValidatorDescriptor(typeof(global::StaticNorth.Valiant.Validation.IPropertyValidator<")
                    .Append(propertyType)
                    .Append(">), typeof(")
                    .Append(propertyValidator.ValidatorType)
                    .AppendLine("), valiantValidationOptions.Lifetime));");
            }
        }

        builder.AppendLine();
        builder.AppendLine("            return services;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        private static global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor CreateValidatorDescriptor(global::System.Type serviceType, global::System.Type implementationType, global::StaticNorth.Valiant.Validation.ValiantValidatorLifetime lifetime)");
        builder.AppendLine("        {");
        builder.AppendLine("            switch (lifetime)");
        builder.AppendLine("            {");
        builder.AppendLine("                case global::StaticNorth.Valiant.Validation.ValiantValidatorLifetime.Scoped:");
        builder.AppendLine("                    return global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Scoped(serviceType, implementationType);");
        builder.AppendLine("                case global::StaticNorth.Valiant.Validation.ValiantValidatorLifetime.Transient:");
        builder.AppendLine("                    return global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Transient(serviceType, implementationType);");
        builder.AppendLine("                default:");
        builder.AppendLine("                    return global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(serviceType, implementationType);");
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

    private static bool HasValidatorReuse(ImmutableArray<ValidationNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case RuleNode ruleNode:
                    if (ruleNode.Rule.Checks.Any(static check => check.Kind == "UseValidator"))
                    {
                        return true;
                    }

                    break;
                case ValidatorReuseNode:
                    return true;
                case IfNode ifNode:
                    if (HasValidatorReuse(ifNode.ThenNodes) || HasValidatorReuse(ifNode.ElseNodes))
                    {
                        return true;
                    }

                    break;
                case SwitchNode switchNode:
                    foreach (var section in switchNode.Sections)
                    {
                        if (HasValidatorReuse(section.Nodes))
                        {
                            return true;
                        }
                    }

                    break;
                case LoopNode loopNode:
                    if (HasValidatorReuse(loopNode.Nodes))
                    {
                        return true;
                    }

                    break;
                case CollectionIterationNode iterationNode:
                    if (HasValidatorReuse(iterationNode.Nodes))
                    {
                        return true;
                    }

                    break;
                case ChildPropertyValidationNode childNode:
                    if (HasValidatorReuse(childNode.Nodes))
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
                case ValidatorReuseNode:
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
                case LoopNode loopNode:
                    count += CountChecks(loopNode.Nodes);
                    break;
                case CollectionIterationNode iterationNode:
                    count += CountChecks(iterationNode.Nodes);
                    break;
                case ChildPropertyValidationNode childNode:
                    count += CountChecks(childNode.Nodes);
                    break;
                case FailureBehaviorNode:
                    break;
                case RawStatementNode:
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
                case ValidatorReuseNode reuseNode:
                    AppendValidatorReuse(builder, namespaceName, indentLevel, reuseNode, ref ruleIndex, totalChecks, emitAsync);
                    break;
                case IfNode ifNode:
                    AppendIfNode(builder, namespaceName, indentLevel, ifNode, ref ruleIndex, totalChecks, emitAsync);
                    break;
                case SwitchNode switchNode:
                    AppendSwitchNode(builder, namespaceName, indentLevel, switchNode, ref ruleIndex, totalChecks, emitAsync);
                    break;
                case LoopNode loopNode:
                    AppendLoopNode(builder, namespaceName, indentLevel, loopNode, ref ruleIndex, totalChecks, emitAsync);
                    break;
                case FlowControlNode flowControlNode:
                    AppendFlowControlNode(builder, namespaceName, indentLevel, flowControlNode);
                    break;
                case CollectionIterationNode iterationNode:
                    AppendCollectionIteration(builder, namespaceName, indentLevel, iterationNode, ref ruleIndex, totalChecks, emitAsync);
                    break;
                case ChildPropertyValidationNode childNode:
                    AppendChildPropertyValidation(builder, namespaceName, indentLevel, childNode, ref ruleIndex, totalChecks, emitAsync);
                    break;
                case FailureBehaviorNode failureBehaviorNode:
                    AppendFailureBehaviorNode(builder, namespaceName, indentLevel, failureBehaviorNode);
                    break;
                case RawStatementNode rawStatementNode:
                    AppendRawStatement(builder, namespaceName, indentLevel, rawStatementNode);
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
        var ruleFailedName = rule.FailureBehaviorExpression == "global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast"
            ? "ruleFailed" + ruleIndex.ToString(CultureInfo.InvariantCulture)
            : null;
        ruleIndex++;

        builder.AppendLine();
        var ruleCondition = GetRuleCondition(rule);
        if (ruleCondition is not null)
        {
            AppendIndent(builder, indentLevel, namespaceName);
            builder.Append("if (").Append(ruleCondition).AppendLine(")");
            AppendIndent(builder, indentLevel, namespaceName);
            builder.AppendLine("{");
            indentLevel++;
        }

        if (ruleFailedName is not null)
        {
            AppendIndent(builder, indentLevel, namespaceName);
            builder.Append("var ").Append(ruleFailedName).AppendLine(" = false;");
        }

        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("var ").Append(localName).Append(" = ").Append(rule.InstanceExpression).Append('.').Append(rule.PropertyAccess).AppendLine(";");

        foreach (var check in rule.Checks)
        {
            AppendCheck(builder, namespaceName, indentLevel, localName, "instance", rule, check, ruleFailedName, totalChecks, emitAsync);
        }

        if (ruleCondition is not null)
        {
            indentLevel--;
            AppendIndent(builder, indentLevel, namespaceName);
            builder.AppendLine("}");
        }
    }

    private static string? GetRuleCondition(RuleModel rule)
    {
        var conditions = ImmutableArray.CreateBuilder<string>();
        if (rule.ConditionExpression is not null)
        {
            conditions.Add(GetRuleConditionInvocation(rule));
        }

        foreach (var parentAccess in rule.ParentPropertyAccesses)
        {
            conditions.Add(rule.InstanceExpression + "." + parentAccess + " is not null");
        }

        return conditions.Count == 0
            ? null
            : string.Join(" && ", conditions);
    }

    private static string? GetParentAccessCondition(string instanceExpression, ImmutableArray<string> parentPropertyAccesses)
    {
        if (parentPropertyAccesses.IsDefaultOrEmpty)
        {
            return null;
        }

        var conditions = ImmutableArray.CreateBuilder<string>(parentPropertyAccesses.Length);
        foreach (var parentAccess in parentPropertyAccesses)
        {
            conditions.Add(instanceExpression + "." + parentAccess + " is not null");
        }

        return string.Join(" && ", conditions);
    }

    private static string GetRuleConditionInvocation(RuleModel rule)
    {
        if (rule.ConditionExpression is null || rule.ConditionDelegateType is null)
        {
            throw new InvalidOperationException("Conditional rule is missing condition metadata.");
        }

        return "((" + rule.ConditionDelegateType + ")(" + rule.ConditionExpression + "))(" + rule.InstanceExpression + ")";
    }

    private static void AppendValidatorReuse(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        ValidatorReuseNode node,
        ref int ruleIndex,
        int totalChecks,
        bool emitAsync)
    {
        var resultName = "reusedResult" + ruleIndex.ToString(CultureInfo.InvariantCulture);
        ruleIndex++;

        builder.AppendLine();
        AppendIndent(builder, indentLevel, namespaceName);
        if (emitAsync)
        {
            builder.Append("var ").Append(resultName).Append(" = await ").Append(node.ValidatorExpression)
                .Append(".ValidateAsync(").Append(node.InstanceExpression).AppendLine(", cancellationToken).ConfigureAwait(false);");
        }
        else
        {
            builder.Append("var ").Append(resultName).Append(" = ").Append(node.ValidatorExpression)
                .Append(".Validate(").Append(node.InstanceExpression).AppendLine(");");
        }

        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("if (").Append(resultName).AppendLine(".Failures.Count > 0)");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("{");
        if (node.PropertyPathPrefixExpression is null)
        {
            AppendIndent(builder, indentLevel + 1, namespaceName);
            builder.Append("(failures ??= new global::System.Collections.Generic.List<global::StaticNorth.Valiant.Validation.ValidationFailure>(validationFailureBehavior == global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast ? 1 : ")
                .Append(totalChecks.ToString(CultureInfo.InvariantCulture))
                .Append(")).AddRange(")
                .Append(resultName)
                .AppendLine(".Failures);");
        }
        else
        {
            var failureName = "reusedFailure" + ruleIndex.ToString(CultureInfo.InvariantCulture);
            var propertyName = "reusedProperty" + ruleIndex.ToString(CultureInfo.InvariantCulture);
            AppendIndent(builder, indentLevel + 1, namespaceName);
            builder.Append("foreach (var ").Append(failureName).Append(" in ").Append(resultName).AppendLine(".Failures)");
            AppendIndent(builder, indentLevel + 1, namespaceName);
            builder.AppendLine("{");
            AppendIndent(builder, indentLevel + 2, namespaceName);
            builder.Append("var ").Append(propertyName).Append(" = ").Append(failureName).AppendLine(".PropertyName;");
            AppendIndent(builder, indentLevel + 2, namespaceName);
            builder.Append("(failures ??= new global::System.Collections.Generic.List<global::StaticNorth.Valiant.Validation.ValidationFailure>(validationFailureBehavior == global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast ? 1 : ")
                .Append(totalChecks.ToString(CultureInfo.InvariantCulture))
                .Append(")).Add(new global::StaticNorth.Valiant.Validation.ValidationFailure(global::System.String.IsNullOrEmpty(")
                .Append(propertyName)
                .Append(") ? (")
                .Append(node.PropertyPathPrefixExpression)
                .Append(").TrimEnd('.') : ")
                .Append(node.PropertyPathPrefixExpression)
                .Append(" + ")
                .Append(propertyName)
                .Append(", ")
                .Append(failureName)
                .Append(".ErrorCode, ")
                .Append(failureName)
                .Append(".ErrorMessage, ")
                .Append(failureName)
                .AppendLine(".Severity));");
            AppendIndent(builder, indentLevel + 1, namespaceName);
            builder.AppendLine("}");
        }
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("}");
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
        }

        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendLoopNode(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        LoopNode node,
        ref int ruleIndex,
        int totalChecks,
        bool emitAsync)
    {
        builder.AppendLine();
        AppendIndent(builder, indentLevel, namespaceName);
        switch (node.Kind)
        {
            case "foreach":
                builder.Append("foreach (").Append(node.Header).AppendLine(")");
                AppendValidationBlock(builder, namespaceName, indentLevel, node.Nodes, ref ruleIndex, totalChecks, emitAsync);
                return;
            case "for":
                builder.Append("for (").Append(node.Header).AppendLine(")");
                AppendValidationBlock(builder, namespaceName, indentLevel, node.Nodes, ref ruleIndex, totalChecks, emitAsync);
                return;
            case "while":
                builder.Append("while (").Append(node.Header).AppendLine(")");
                AppendValidationBlock(builder, namespaceName, indentLevel, node.Nodes, ref ruleIndex, totalChecks, emitAsync);
                return;
            case "do":
                builder.AppendLine("do");
                AppendValidationBlock(builder, namespaceName, indentLevel, node.Nodes, ref ruleIndex, totalChecks, emitAsync);
                AppendIndent(builder, indentLevel, namespaceName);
                builder.Append("while (").Append(node.Condition).AppendLine(");");
                return;
            default:
                throw new InvalidOperationException("Unsupported loop kind '" + node.Kind + "'.");
        }
    }

    private static void AppendFlowControlNode(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        FlowControlNode node)
    {
        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append(node.Kind).AppendLine(";");
    }

    private static void AppendFailureBehaviorNode(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        FailureBehaviorNode node)
    {
        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("validationFailureBehavior = ").Append(node.BehaviorExpression).AppendLine(";");
    }

    private static void AppendRawStatement(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        RawStatementNode node)
    {
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine(node.Statement);
    }

    private static void AppendCollectionIteration(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        CollectionIterationNode node,
        ref int ruleIndex,
        int totalChecks,
        bool emitAsync)
    {
        builder.AppendLine();
        var parentCondition = GetParentAccessCondition(node.InstanceExpression, node.ParentPropertyAccesses);
        if (parentCondition is not null)
        {
            AppendIndent(builder, indentLevel, namespaceName);
            builder.Append("if (").Append(parentCondition).AppendLine(")");
            AppendIndent(builder, indentLevel, namespaceName);
            builder.AppendLine("{");
            indentLevel++;
        }

        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("var ").Append(node.CollectionLocalName).Append(" = ").Append(node.CollectionExpression).AppendLine(";");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("if (").Append(node.CollectionLocalName).AppendLine(" is not null)");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, indentLevel + 1, namespaceName);
        builder.Append("var ").Append(node.IndexLocalName).AppendLine(" = 0;");
        AppendIndent(builder, indentLevel + 1, namespaceName);
        builder.Append("foreach (var ").Append(node.ItemLocalName).Append(" in ").Append(node.CollectionLocalName).AppendLine(")");
        AppendIndent(builder, indentLevel + 1, namespaceName);
        builder.AppendLine("{");
        AppendValidationNodes(builder, namespaceName, indentLevel + 2, node.Nodes, ref ruleIndex, totalChecks, emitAsync);
        AppendIndent(builder, indentLevel + 2, namespaceName);
        builder.Append(node.IndexLocalName).AppendLine("++;");
        AppendIndent(builder, indentLevel + 1, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("}");
        if (parentCondition is not null)
        {
            indentLevel--;
            AppendIndent(builder, indentLevel, namespaceName);
            builder.AppendLine("}");
        }
    }

    private static void AppendChildPropertyValidation(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        ChildPropertyValidationNode node,
        ref int ruleIndex,
        int totalChecks,
        bool emitAsync)
    {
        builder.AppendLine();
        var parentCondition = GetParentAccessCondition(node.InstanceExpression, node.ParentPropertyAccesses);
        if (parentCondition is not null)
        {
            AppendIndent(builder, indentLevel, namespaceName);
            builder.Append("if (").Append(parentCondition).AppendLine(")");
            AppendIndent(builder, indentLevel, namespaceName);
            builder.AppendLine("{");
            indentLevel++;
        }

        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("var ").Append(node.ChildLocalName).Append(" = ").Append(node.ChildExpression).AppendLine(";");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("if (").Append(node.ChildLocalName).AppendLine(" is not null)");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("{");
        AppendValidationNodes(builder, namespaceName, indentLevel + 1, node.Nodes, ref ruleIndex, totalChecks, emitAsync);
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("}");

        if (parentCondition is not null)
        {
            indentLevel--;
            AppendIndent(builder, indentLevel, namespaceName);
            builder.AppendLine("}");
        }
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
        string? ruleFailedName,
        int totalChecks,
        bool emitAsync)
    {
        if (check.IsAsync && !emitAsync)
        {
            return;
        }

        if (check.Kind == "UseValidator")
        {
            AppendPropertyValidatorReuseCheck(builder, namespaceName, indentLevel, localName, rule, check, ruleFailedName, totalChecks, emitAsync);
            return;
        }

        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("if (");
        if (ruleFailedName is not null)
        {
            builder.Append('!').Append(ruleFailedName).Append(" && ");
        }

        builder.Append(GetFailureCondition(localName, instanceName, rule, check, emitAsync)).AppendLine(")");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("{");
        if (ruleFailedName is not null)
        {
            AppendIndent(builder, indentLevel + 1, namespaceName);
            builder.Append(ruleFailedName).AppendLine(" = true;");
        }

        AppendIndent(builder, indentLevel + 1, namespaceName);
        builder.Append("(failures ??= new global::System.Collections.Generic.List<global::StaticNorth.Valiant.Validation.ValidationFailure>(validationFailureBehavior == global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast ? 1 : ")
            .Append(totalChecks.ToString(CultureInfo.InvariantCulture))
            .Append(")).Add(new global::StaticNorth.Valiant.Validation.ValidationFailure(")
            .Append(rule.PropertyNameExpression)
            .Append(", ")
            .Append(GetFailureCodeExpression(check))
            .Append(", ")
            .Append(GetFailureMessageExpression(rule, check))
            .Append(", ")
            .Append(GetFailureSeverityExpression(check))
            .AppendLine("));");
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
        builder.AppendLine("return new global::StaticNorth.Valiant.Validation.ValidationResult(failures);");
        AppendIndent(builder, indentLevel + 1, namespaceName);
        builder.AppendLine("}");
        AppendIndent(builder, indentLevel, namespaceName);
        builder.AppendLine("}");
    }

    private static void AppendPropertyValidatorReuseCheck(
        StringBuilder builder,
        string? namespaceName,
        int indentLevel,
        string localName,
        RuleModel rule,
        RuleCheck check,
        string? ruleFailedName,
        int totalChecks,
        bool emitAsync)
    {
        var resultName = "propertyValidatorResult" + localName.Substring("value".Length);
        var failureName = "propertyValidatorFailure" + localName.Substring("value".Length);

        AppendIndent(builder, indentLevel, namespaceName);
        builder.Append("if (");
        if (ruleFailedName is not null)
        {
            builder.Append('!').Append(ruleFailedName).Append(" && ");
        }

        builder.Append(localName).AppendLine(" is not null)");
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
        if (ruleFailedName is not null)
        {
            AppendIndent(builder, indentLevel + 2, namespaceName);
            builder.Append(ruleFailedName).AppendLine(" = true;");
        }

        AppendIndent(builder, indentLevel + 2, namespaceName);
        builder.Append("foreach (var ").Append(failureName).Append(" in ").Append(resultName).AppendLine(".Failures)");
        AppendIndent(builder, indentLevel + 2, namespaceName);
        builder.AppendLine("{");
        AppendIndent(builder, indentLevel + 3, namespaceName);
        builder.Append("(failures ??= new global::System.Collections.Generic.List<global::StaticNorth.Valiant.Validation.ValidationFailure>(validationFailureBehavior == global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast ? 1 : ")
            .Append(totalChecks.ToString(CultureInfo.InvariantCulture))
            .Append(")).Add(new global::StaticNorth.Valiant.Validation.ValidationFailure(CombinePropertyPath(")
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
        builder.AppendLine("return new global::StaticNorth.Valiant.Validation.ValidationResult(failures);");
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

    private static string GetFailureCondition(string localName, string instanceName, RuleModel rule, RuleCheck check, bool emitAsync)
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

                return "global::System.Collections.Generic.EqualityComparer<" + rule.PropertyType + ">.Default.Equals(" + localName + ", default!)";
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
            case "All":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.All(" + localName + ", (" + check.DelegateType + ")(" + GetCustomPredicateExpression(check) + "))";
            case "Any":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.Any(" + localName + ", (" + check.DelegateType + ")(" + GetCustomPredicateExpression(check) + "))";
            case "None":
                return "!global::StaticNorth.Valiant.Validation.ValidationChecks.None(" + localName + ", (" + check.DelegateType + ")(" + GetCustomPredicateExpression(check) + "))";
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
            case "Satisfies":
                return "!" + GetCustomPredicateInvocation(localName, instanceName, check, false);
            case "SatisfiesAsync":
                if (!emitAsync)
                {
                    throw new InvalidOperationException("Cannot emit async custom validation in a synchronous method.");
                }

                return "!" + GetCustomPredicateInvocation(localName, instanceName, check, true);
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
        return check.Arguments[0];
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

    private static string GetDefaultMessageExpression(RuleModel rule, RuleCheck check)
    {
        if (rule.PropertyNameExpression == ToLiteral(rule.PropertyName) &&
            !RequiresRuntimeArgumentFormatting(check.Kind))
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
