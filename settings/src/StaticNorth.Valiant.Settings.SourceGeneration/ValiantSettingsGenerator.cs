using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StaticNorth.Valiant.SourceGeneration;

namespace StaticNorth.Valiant.Settings.SourceGeneration;

/// <summary>
/// Generates schema metadata for Valiant settings models.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ValiantSettingsGenerator : IIncrementalGenerator
{
    private const int DictionaryLookupThreshold = 8;
    private const string SettingsAttributeMetadataName = "StaticNorth.Valiant.Settings.ValiantSettingsAttribute";
    private const string SecretAttributeMetadataName = "StaticNorth.Valiant.Settings.ValiantSecretAttribute";

    private static readonly SymbolDisplayFormat SourceTypeDisplayFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    private static readonly SymbolDisplayFormat SchemaTypeDisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly DiagnosticDescriptor GenericSettingsModelNotSupported = new(
        "VLS001",
        "Settings model must not be generic",
        "Settings model '{0}' must not be generic because Valiant generates concrete schema provider types",
        "StaticNorth.Valiant.Settings",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor SettingsSectionPathRequired = new(
        "VLS002",
        "Settings model section path is required",
        "Settings model '{0}' must provide a non-empty configuration section path",
        "StaticNorth.Valiant.Settings",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var settingsModels = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                SettingsAttributeMetadataName,
                static (node, _) => IsSettingsCandidate(node),
                static (context, cancellationToken) => CreateSettingsModel(context, cancellationToken))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        var validSettingsModels = settingsModels
            .Where(static model => model.IsValid)
            .WithComparer(SettingsModelComparer.Instance)
            .WithTrackingName("Settings.Model");
        var schemaProviders = validSettingsModels
            .Select(static (model, _) => new SettingsSchemaProviderRegistrationModel(model.FullTypeName, model.ProviderTypeName))
            .Collect()
            .WithTrackingName("Settings.SchemaProviders");
        var settingsRegistrations = validSettingsModels
            .Select(static (model, _) => new SettingsRegistrationModel(
                model.FullTypeName,
                model.SectionPath,
                model.CanConfigureOptions,
                model.ProviderTypeName))
            .Collect()
            .WithTrackingName("Settings.Registrations");
        var settingsRegistrationFeatures = context.CompilationProvider
            .Select(static (compilation, _) => GetSettingsRegistrationFeatures(compilation));

        context.RegisterSourceOutput(settingsModels, static (context, model) => ExecuteDiagnostics(context, model));
        context.RegisterSourceOutput(validSettingsModels, static (context, model) => ExecuteSchemaProvider(context, model));
        context.RegisterSourceOutput(schemaProviders, static (context, models) => ExecuteSchemaRegistry(context, models));
        context.RegisterSourceOutput(
            settingsRegistrationFeatures.Combine(settingsRegistrations),
            static (context, source) => ExecuteSettingsRegistration(context, source.Left, source.Right));
    }

    private static bool IsSettingsCandidate(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { AttributeLists.Count: > 0 } ||
            node is StructDeclarationSyntax { AttributeLists.Count: > 0 } ||
            node is RecordDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    private static SettingsModel? CreateSettingsModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol settingsType)
        {
            return null;
        }

        var typeSyntax = (TypeDeclarationSyntax)context.TargetNode;
        var fullTypeName = settingsType.ToDisplayString(SourceTypeDisplayFormat);
        if (settingsType.TypeParameters.Length > 0)
        {
            return SettingsModel.Invalid(
                GenericSettingsModelNotSupported,
                settingsType.Name,
                fullTypeName,
                typeSyntax.Identifier.GetLocation());
        }

        var attribute = GetSettingsAttribute(context.Attributes, cancellationToken);
        if (string.IsNullOrWhiteSpace(attribute.SectionPath))
        {
            return SettingsModel.Invalid(
                SettingsSectionPathRequired,
                settingsType.Name,
                fullTypeName,
                attribute.Location ?? typeSyntax.Identifier.GetLocation());
        }

        var properties = CreatePropertyModels(settingsType, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), cancellationToken);

        return SettingsModel.Valid(
            settingsType.Name,
            fullTypeName,
            attribute.SectionPath,
            settingsType.IsReferenceType && attribute.Configure,
            CreateProviderTypeName(fullTypeName),
            properties);
    }

    private static void ExecuteDiagnostics(SourceProductionContext context, SettingsModel model)
    {
        if (model.IsValid)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(model.Diagnostic, model.Location.ToLocation(), model.Name));
    }

    private static void ExecuteSchemaProvider(SourceProductionContext context, SettingsModel model)
    {
        var builder = new StringBuilder();
        ValiantSourceBuilder.AppendGeneratedFileHeader(builder);
        builder.AppendLine();
        builder.AppendLine("namespace StaticNorth.Valiant.Settings.Generated");
        builder.AppendLine("{");
        builder.Append("    internal sealed class ");
        builder.Append(model.ProviderTypeName);
        builder.AppendLine(" : global::StaticNorth.Valiant.Settings.IValiantSettingsSchemaProvider");
        builder.AppendLine("    {");
        builder.Append("        public global::System.Type SettingsType => typeof(");
        builder.Append(model.FullTypeName);
        builder.AppendLine(");");
        builder.AppendLine();
        builder.Append("        public string? SectionPath => ");
        builder.Append(model.SectionPath is null ? "null" : ToLiteral(model.SectionPath));
        builder.AppendLine(";");
        builder.AppendLine();
        builder.AppendLine("        public global::StaticNorth.Valiant.Settings.ValiantSettingsSchema Schema { get; } =");
        builder.AppendLine("            new global::StaticNorth.Valiant.Settings.ValiantSettingsSchema(");
        builder.Append("                typeof(");
        builder.Append(model.FullTypeName);
        builder.AppendLine("),");
        builder.Append("                ");
        builder.Append(ToLiteral(model.Name));
        builder.AppendLine(",");
        builder.Append("                ");
        builder.Append(model.SectionPath is null ? "null" : ToLiteral(model.SectionPath));
        builder.AppendLine(",");
        builder.Append("                ");
        AppendPropertyArray(builder, model.Properties, "                ");
        builder.AppendLine(");");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        context.AddSource(model.ProviderTypeName + ".ValiantSettingsSchema.g.cs", builder.ToString());
    }

    private static void ExecuteSchemaRegistry(
        SourceProductionContext context,
        ImmutableArray<SettingsSchemaProviderRegistrationModel> models)
    {
        if (models.IsEmpty)
        {
            return;
        }

        var orderedModels = models
            .OrderBy(static model => model.FullTypeName, StringComparer.Ordinal)
            .ToImmutableArray();

        var builder = new StringBuilder();
        ValiantSourceBuilder.AppendGeneratedFileHeader(builder);
        builder.AppendLine();
        builder.AppendLine("namespace StaticNorth.Valiant.Settings");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Provides access to source-generated Valiant settings schemas in the current compilation.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    public static class ValiantSettingsSchemas");
        builder.AppendLine("    {");
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Gets all generated Valiant settings schema providers in the current compilation.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::StaticNorth.Valiant.Settings.IValiantSettingsSchemaProvider> All { get; } =");
        builder.AppendLine("            new global::StaticNorth.Valiant.Settings.IValiantSettingsSchemaProvider[]");
        builder.AppendLine("            {");
        foreach (var model in orderedModels)
        {
            builder.Append("                new global::StaticNorth.Valiant.Settings.Generated.");
            builder.Append(model.ProviderTypeName);
            builder.AppendLine("(),");
        }

        builder.AppendLine("            };");
        if (orderedModels.Length > DictionaryLookupThreshold)
        {
            builder.AppendLine();
            builder.AppendLine("        private static readonly global::System.Collections.Generic.IReadOnlyDictionary<global::System.Type, global::StaticNorth.Valiant.Settings.IValiantSettingsSchemaProvider> ProvidersByType =");
            builder.AppendLine("            new global::System.Collections.Generic.Dictionary<global::System.Type, global::StaticNorth.Valiant.Settings.IValiantSettingsSchemaProvider>");
            builder.AppendLine("            {");
            for (var index = 0; index < orderedModels.Length; index++)
            {
                builder.Append("                [typeof(");
                builder.Append(orderedModels[index].FullTypeName);
                builder.Append(")] = All[");
                builder.Append(index.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("],");
            }

            builder.AppendLine("            };");
        }

        builder.AppendLine();
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Gets the generated schema provider for the specified settings model type.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        public static global::StaticNorth.Valiant.Settings.IValiantSettingsSchemaProvider Get<TSettings>()");
        builder.AppendLine("        {");
        builder.AppendLine("            if (TryGet<TSettings>(out var provider))");
        builder.AppendLine("            {");
        builder.AppendLine("                return provider!;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            throw new global::System.InvalidOperationException(\"No Valiant settings schema was generated for \" + typeof(TSettings).FullName + \".\");");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Tries to get the generated schema provider for the specified settings model type.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        public static bool TryGet<TSettings>(out global::StaticNorth.Valiant.Settings.IValiantSettingsSchemaProvider? provider)");
        builder.AppendLine("        {");
        if (orderedModels.Length > DictionaryLookupThreshold)
        {
            builder.AppendLine("            return ProvidersByType.TryGetValue(typeof(TSettings), out provider);");
        }
        else
        {
            builder.AppendLine("            var settingsType = typeof(TSettings);");
            builder.AppendLine("            foreach (var candidate in All)");
            builder.AppendLine("            {");
            builder.AppendLine("                if (candidate.SettingsType == settingsType)");
            builder.AppendLine("                {");
            builder.AppendLine("                    provider = candidate;");
            builder.AppendLine("                    return true;");
            builder.AppendLine("                }");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            provider = null;");
            builder.AppendLine("            return false;");
        }
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        context.AddSource("ValiantSettingsSchemas.g.cs", builder.ToString());
    }

    private static SettingsAttributeModel GetSettingsAttribute(ImmutableArray<AttributeData> attributes, CancellationToken cancellationToken)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass?.ToDisplayString() != SettingsAttributeMetadataName)
            {
                continue;
            }

            var sectionPath = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string
                : null;
            var configure = attribute.ConstructorArguments.Length > 1 &&
                            attribute.ConstructorArguments[1].Value is true;
            var location = (attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) as AttributeSyntax)
                ?.ArgumentList
                ?.Arguments
                .FirstOrDefault()
                ?.GetLocation();

            foreach (var namedArgument in attribute.NamedArguments)
            {
                if (string.Equals(namedArgument.Key, "Configure", StringComparison.Ordinal) &&
                    namedArgument.Value.Value is bool namedConfigure)
                {
                    configure = namedConfigure;
                }
            }

            return new SettingsAttributeModel(sectionPath, configure, location);
        }

        return new SettingsAttributeModel(null, false, null);
    }

    private static SettingsRegistrationFeatures GetSettingsRegistrationFeatures(Compilation compilation)
    {
        var hasSettingsRegistration = ValiantDependencyInjectionSymbols.HasDependencyInjection(compilation) &&
            compilation.GetTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration") is not null &&
            compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.OptionsConfigurationServiceCollectionExtensions") is not null;

        var hasValidationIntegration = compilation.GetTypeByMetadataName("StaticNorth.Valiant.Settings.Validation.ValiantSettingsValidationOptions") is not null &&
            compilation.GetTypeByMetadataName("StaticNorth.Valiant.Settings.Validation.ValiantSettingsValidationServiceCollectionExtensions") is not null;

        return new SettingsRegistrationFeatures(hasSettingsRegistration, hasValidationIntegration);
    }

    private static void ExecuteSettingsRegistration(
        SourceProductionContext context,
        SettingsRegistrationFeatures features,
        ImmutableArray<SettingsRegistrationModel> models)
    {
        if (!features.HasSettingsRegistration ||
            models.IsEmpty)
        {
            return;
        }

        var orderedModels = models
            .OrderBy(static model => model.FullTypeName, StringComparer.Ordinal)
            .ToImmutableArray();
        var sectionModels = orderedModels
            .Where(static model => model.SectionPath is not null && model.CanConfigureOptions)
            .ToImmutableArray();

        var builder = new StringBuilder();
        ValiantSourceBuilder.AppendGeneratedFileHeader(builder);
        builder.AppendLine();
        builder.AppendLine("namespace StaticNorth.Valiant.Settings.DependencyInjection");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Provides dependency injection registration for source-generated Valiant settings schemas.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    public static class ValiantSettingsServiceCollectionExtensions");
        builder.AppendLine("    {");
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Registers generated Valiant settings schema providers with default options.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddValiantSettings(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        builder.AppendLine("        {");
        builder.AppendLine("            return AddValiantSettings(services, configure: null);");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Registers generated Valiant settings schema providers with configured options.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddValiantSettings(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, global::System.Action<global::StaticNorth.Valiant.Settings.ValiantSettingsOptions>? configure)");
        builder.AppendLine("        {");
        builder.AppendLine("            var valiantSettingsOptions = new global::StaticNorth.Valiant.Settings.ValiantSettingsOptions();");
        builder.AppendLine("            configure?.Invoke(valiantSettingsOptions);");
        builder.AppendLine();
        builder.AppendLine("            AddValiantSettingsCore(services, valiantSettingsOptions);");
        builder.AppendLine();
        builder.AppendLine("            return services;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Registers generated Valiant settings schema providers and configures settings models that opt into binding with default options.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddValiantSettings(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, global::Microsoft.Extensions.Configuration.IConfiguration configuration)");
        builder.AppendLine("        {");
        builder.AppendLine("            return AddValiantSettings(services, configuration, configure: null);");
        builder.AppendLine("        }");
        if (features.HasValidationIntegration)
        {
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Registers generated Valiant settings schema providers, configures settings models that opt into binding, and optionally validates them with Valiant validators.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddValiantSettings(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, global::Microsoft.Extensions.Configuration.IConfiguration configuration, global::System.Action<global::StaticNorth.Valiant.Settings.Validation.ValiantSettingsValidationOptions>? configure)");
            builder.AppendLine("        {");
            builder.AppendLine("            _ = configuration ?? throw new global::System.ArgumentNullException(nameof(configuration));");
            builder.AppendLine();
            builder.AppendLine("            var valiantSettingsValidationOptions = new global::StaticNorth.Valiant.Settings.Validation.ValiantSettingsValidationOptions();");
            builder.AppendLine("            configure?.Invoke(valiantSettingsValidationOptions);");
            builder.AppendLine();
            builder.AppendLine("            AddValiantSettingsCore(services, valiantSettingsValidationOptions);");
            AppendSectionOptionsConfiguration(builder, sectionModels);
            foreach (var model in sectionModels)
            {
                builder.Append("            global::StaticNorth.Valiant.Settings.Validation.ValiantSettingsValidationServiceCollectionExtensions.AddValiantSettingsOptionsValidation<");
                builder.Append(model.FullTypeName);
                builder.AppendLine(">(services, valiantSettingsValidationOptions);");
            }

            builder.AppendLine();
            builder.AppendLine("            return services;");
            builder.AppendLine("        }");
        }
        else
        {
            builder.AppendLine();
            builder.AppendLine("        /// <summary>");
            builder.AppendLine("        /// Registers generated Valiant settings schema providers and configures settings models that opt into binding with configured options.");
            builder.AppendLine("        /// </summary>");
            builder.AppendLine("        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddValiantSettings(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, global::Microsoft.Extensions.Configuration.IConfiguration configuration, global::System.Action<global::StaticNorth.Valiant.Settings.ValiantSettingsOptions>? configure)");
            builder.AppendLine("        {");
            builder.AppendLine("            _ = configuration ?? throw new global::System.ArgumentNullException(nameof(configuration));");
            builder.AppendLine();
            builder.AppendLine("            var valiantSettingsOptions = new global::StaticNorth.Valiant.Settings.ValiantSettingsOptions();");
            builder.AppendLine("            configure?.Invoke(valiantSettingsOptions);");
            builder.AppendLine();
            builder.AppendLine("            AddValiantSettingsCore(services, valiantSettingsOptions);");
            AppendSectionOptionsConfiguration(builder, sectionModels);
            builder.AppendLine();
            builder.AppendLine("            return services;");
            builder.AppendLine("        }");
        }

        builder.AppendLine();
        builder.AppendLine("        private static void AddValiantSettingsCore(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, global::StaticNorth.Valiant.Settings.ValiantSettingsOptions options)");
        builder.AppendLine("        {");
        builder.AppendLine("            _ = services ?? throw new global::System.ArgumentNullException(nameof(services));");
        builder.AppendLine("            _ = options ?? throw new global::System.ArgumentNullException(nameof(options));");
        builder.AppendLine();
        builder.AppendLine("            if (!options.RegisterSchemaProviders)");
        builder.AppendLine("            {");
        builder.AppendLine("                return;");
        builder.AppendLine("            }");
        builder.AppendLine();
        AppendSchemaProviderRegistrations(builder, orderedModels);
        builder.AppendLine("        }");

        builder.AppendLine("    }");
        builder.AppendLine("}");

        context.AddSource("StaticNorth.Valiant.Settings.DependencyInjection.g.cs", builder.ToString());
    }

    private static void AppendSectionOptionsConfiguration(
        StringBuilder builder,
        ImmutableArray<SettingsRegistrationModel> sectionModels)
    {
        foreach (var model in sectionModels)
        {
            builder.Append("            global::Microsoft.Extensions.DependencyInjection.OptionsConfigurationServiceCollectionExtensions.Configure<");
            builder.Append(model.FullTypeName);
            builder.Append(">(services, configuration.GetSection(");
            builder.Append(ToLiteral(model.SectionPath!));
            builder.AppendLine("));");
        }
    }

    private static void AppendSchemaProviderRegistrations(
        StringBuilder builder,
        ImmutableArray<SettingsRegistrationModel> models)
    {
        foreach (var model in models)
        {
            builder.Append("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(global::StaticNorth.Valiant.Settings.IValiantSettingsSchemaProvider), typeof(global::StaticNorth.Valiant.Settings.Generated.");
            builder.Append(model.ProviderTypeName);
            builder.AppendLine(")));");
        }
    }

    private static ImmutableArray<SettingsPropertyModel> CreatePropertyModels(
        INamedTypeSymbol type,
        HashSet<ITypeSymbol> visitedTypes,
        CancellationToken cancellationToken)
    {
        var properties = ImmutableArray.CreateBuilder<SettingsPropertyModel>();
        foreach (var member in type.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (member is IPropertySymbol property &&
                IsSettingsProperty(property))
            {
                properties.Add(CreatePropertyModel(
                    property.Type,
                    property.Name,
                    property.Name,
                    property.IsRequired,
                    HasSecretAttribute(property),
                    visitedTypes,
                    cancellationToken));
            }
        }

        return properties.ToImmutable();
    }

    private static bool IsSettingsProperty(IPropertySymbol property)
    {
        return property is { IsStatic: false, Parameters.Length: 0, GetMethod: not null, DeclaredAccessibility: Accessibility.Public };
    }

    private static SettingsPropertyModel CreatePropertyModel(
        ITypeSymbol type,
        string name,
        string configurationKey,
        bool isExplicitlyRequired,
        bool isSecret,
        HashSet<ITypeSymbol> visitedTypes,
        CancellationToken cancellationToken)
    {
        var effectiveType = UnwrapNullable(type);
        var kind = GetPropertyKind(effectiveType);
        var itemType = GetCollectionItemType(effectiveType);
        var item = itemType is null
            ? null
            : CreateItemModel(itemType, visitedTypes, cancellationToken);
        var nestedProperties = ImmutableArray<SettingsPropertyModel>.Empty;

        if (kind == SettingsPropertyKind.Object &&
            effectiveType is INamedTypeSymbol namedType &&
            visitedTypes.Add(namedType))
        {
            nestedProperties = CreatePropertyModels(namedType, visitedTypes, cancellationToken);
            visitedTypes.Remove(namedType);
        }

        return new SettingsPropertyModel(
            name,
            configurationKey,
            effectiveType.ToDisplayString(SchemaTypeDisplayFormat),
            kind,
            IsRequired(type, isExplicitlyRequired),
            IsNullable(type),
            isSecret,
            null,
            item,
            nestedProperties);
    }

    private static SettingsPropertyModel CreateItemModel(
        ITypeSymbol type,
        HashSet<ITypeSymbol> visitedTypes,
        CancellationToken cancellationToken)
    {
        var effectiveType = UnwrapNullable(type);
        var kind = GetPropertyKind(effectiveType);
        var nestedProperties = ImmutableArray<SettingsPropertyModel>.Empty;
        if (kind == SettingsPropertyKind.Object &&
            effectiveType is INamedTypeSymbol namedType &&
            visitedTypes.Add(namedType))
        {
            nestedProperties = CreatePropertyModels(namedType, visitedTypes, cancellationToken);
            visitedTypes.Remove(namedType);
        }

        return new SettingsPropertyModel(
            "$item",
            "$item",
            effectiveType.ToDisplayString(SchemaTypeDisplayFormat),
            kind,
            false,
            IsNullable(type),
            false,
            null,
            null,
            nestedProperties);
    }

    private static bool HasSecretAttribute(IPropertySymbol property)
    {
        foreach (var attribute in property.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == SecretAttributeMetadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: 1
            } nullableType)
        {
            return nullableType.TypeArguments[0];
        }

        return type;
    }

    private static ITypeSymbol? GetCollectionItemType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return null;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return null;
        }

        foreach (var interfaceType in namedType.AllInterfaces)
        {
            if (interfaceType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T &&
                interfaceType.TypeArguments.Length == 1)
            {
                return interfaceType.TypeArguments[0];
            }
        }

        return null;
    }

    private static SettingsPropertyKind GetPropertyKind(ITypeSymbol type)
    {
        if (GetCollectionItemType(type) is not null)
        {
            return SettingsPropertyKind.Array;
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return SettingsPropertyKind.Enum;
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_String:
                return SettingsPropertyKind.String;
            case SpecialType.System_Boolean:
                return SettingsPropertyKind.Boolean;
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                return SettingsPropertyKind.Integer;
            case SpecialType.System_Decimal:
            case SpecialType.System_Double:
            case SpecialType.System_Single:
                return SettingsPropertyKind.Number;
        }

        if (type is INamedTypeSymbol namedType &&
            !IsSystemType(namedType) &&
            (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct))
        {
            return SettingsPropertyKind.Object;
        }

        return SettingsPropertyKind.Unknown;
    }

    private static bool IsSystemType(INamedTypeSymbol type)
    {
        var containingNamespace = type.ContainingNamespace?.ToDisplayString();
        return containingNamespace is not null &&
            containingNamespace.StartsWith("System", StringComparison.Ordinal);
    }

    private static bool IsRequired(ITypeSymbol type, bool isExplicitlyRequired)
    {
        if (isExplicitlyRequired)
        {
            return true;
        }

        return type is { IsReferenceType: true, NullableAnnotation: NullableAnnotation.NotAnnotated };
    }

    private static bool IsNullable(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
    }

    private static void AppendPropertyArray(
        StringBuilder builder,
        ImmutableArray<SettingsPropertyModel> properties,
        string indent)
    {
        builder.AppendLine("new global::StaticNorth.Valiant.Settings.ValiantSettingsPropertySchema[]");
        builder.Append(indent);
        builder.AppendLine("{");
        foreach (var property in properties)
        {
            builder.Append(indent);
            builder.Append("    ");
            AppendPropertySchema(builder, property, indent + "    ");
            builder.AppendLine(",");
        }

        builder.Append(indent);
        builder.Append("}");
    }

    private static void AppendPropertySchema(
        StringBuilder builder,
        SettingsPropertyModel property,
        string indent)
    {
        builder.AppendLine("new global::StaticNorth.Valiant.Settings.ValiantSettingsPropertySchema(");
        builder.Append(indent);
        builder.Append("    ");
        builder.Append(ToLiteral(property.Name));
        builder.AppendLine(",");
        builder.Append(indent);
        builder.Append("    ");
        builder.Append(ToLiteral(property.ConfigurationKey));
        builder.AppendLine(",");
        builder.Append(indent);
        builder.Append("    ");
        builder.Append(ToLiteral(property.TypeName));
        builder.AppendLine(",");
        builder.Append(indent);
        builder.Append("    global::StaticNorth.Valiant.Settings.ValiantSettingsPropertyKind.");
        builder.Append(property.Kind);
        builder.AppendLine(",");
        builder.Append(indent);
        builder.Append("    ");
        builder.Append(property.IsRequired ? "true" : "false");
        builder.AppendLine(",");
        builder.Append(indent);
        builder.Append("    ");
        builder.Append(property.IsNullable ? "true" : "false");
        builder.AppendLine(",");
        builder.Append(indent);
        builder.Append("    ");
        builder.Append(property.IsSecret ? "true" : "false");
        builder.AppendLine(",");
        builder.Append(indent);
        builder.Append("    ");
        builder.Append(property.DefaultValue is null ? "null" : ToLiteral(property.DefaultValue));
        builder.AppendLine(",");
        builder.Append(indent);
        builder.Append("    ");
        if (property.Item is null)
        {
            builder.AppendLine("null,");
        }
        else
        {
            AppendPropertySchema(builder, property.Item, indent + "    ");
            builder.AppendLine(",");
        }

        builder.Append(indent);
        builder.Append("    ");
        AppendPropertyArray(builder, property.Properties, indent + "    ");
        builder.Append(")");
    }

    private static string CreateProviderTypeName(string fullTypeName)
    {
        var sanitizedName = new StringBuilder();
        foreach (var ch in fullTypeName.Replace("global::", string.Empty))
        {
            sanitizedName.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return "__ValiantSettingsSchemaProvider_" + sanitizedName + "_" + GetStableHash(fullTypeName);
    }

    private static string GetStableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in value)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return hash.ToString("x8");
        }
    }

    private static string ToLiteral(string value)
    {
        return ValiantSourceLiterals.String(value);
    }

    private readonly struct SettingsSchemaProviderRegistrationModel(
        string fullTypeName,
        string providerTypeName) : IEquatable<SettingsSchemaProviderRegistrationModel>
    {
        public string FullTypeName { get; } = fullTypeName;

        public string ProviderTypeName { get; } = providerTypeName;

        public bool Equals(SettingsSchemaProviderRegistrationModel other)
        {
            return string.Equals(FullTypeName, other.FullTypeName, StringComparison.Ordinal) &&
                string.Equals(ProviderTypeName, other.ProviderTypeName, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is SettingsSchemaProviderRegistrationModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StringComparer.Ordinal.GetHashCode(FullTypeName);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(ProviderTypeName);
                return hashCode;
            }
        }
    }

    private readonly struct SettingsAttributeModel(string? sectionPath, bool configure, Location? location)
    {
        public string? SectionPath { get; } = sectionPath;

        public bool Configure { get; } = configure;

        public Location? Location { get; } = location;
    }

    private readonly struct SettingsRegistrationModel(
        string fullTypeName,
        string? sectionPath,
        bool canConfigureOptions,
        string providerTypeName) : IEquatable<SettingsRegistrationModel>
    {
        public string FullTypeName { get; } = fullTypeName;

        public string? SectionPath { get; } = sectionPath;

        public bool CanConfigureOptions { get; } = canConfigureOptions;

        public string ProviderTypeName { get; } = providerTypeName;

        public bool Equals(SettingsRegistrationModel other)
        {
            return string.Equals(FullTypeName, other.FullTypeName, StringComparison.Ordinal) &&
                string.Equals(SectionPath, other.SectionPath, StringComparison.Ordinal) &&
                CanConfigureOptions == other.CanConfigureOptions &&
                string.Equals(ProviderTypeName, other.ProviderTypeName, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is SettingsRegistrationModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StringComparer.Ordinal.GetHashCode(FullTypeName);
                hashCode = (hashCode * 397) ^ (SectionPath is null ? 0 : StringComparer.Ordinal.GetHashCode(SectionPath));
                hashCode = (hashCode * 397) ^ CanConfigureOptions.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(ProviderTypeName);
                return hashCode;
            }
        }
    }

    private readonly struct SettingsRegistrationFeatures(
        bool hasSettingsRegistration,
        bool hasValidationIntegration) : IEquatable<SettingsRegistrationFeatures>
    {
        public bool HasSettingsRegistration { get; } = hasSettingsRegistration;

        public bool HasValidationIntegration { get; } = hasValidationIntegration;

        public bool Equals(SettingsRegistrationFeatures other)
        {
            return HasSettingsRegistration == other.HasSettingsRegistration &&
                HasValidationIntegration == other.HasValidationIntegration;
        }

        public override bool Equals(object? obj)
        {
            return obj is SettingsRegistrationFeatures other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (HasSettingsRegistration.GetHashCode() * 397) ^ HasValidationIntegration.GetHashCode();
            }
        }
    }

    private sealed class SettingsModelComparer : IEqualityComparer<SettingsModel>
    {
        public static readonly SettingsModelComparer Instance = new();

        private SettingsModelComparer()
        {
        }

        public bool Equals(SettingsModel? x, SettingsModel? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.IsValid == y.IsValid &&
                string.Equals(x.Name, y.Name, StringComparison.Ordinal) &&
                string.Equals(x.FullTypeName, y.FullTypeName, StringComparison.Ordinal) &&
                string.Equals(x.SectionPath, y.SectionPath, StringComparison.Ordinal) &&
                x.CanConfigureOptions == y.CanConfigureOptions &&
                string.Equals(x.ProviderTypeName, y.ProviderTypeName, StringComparison.Ordinal) &&
                SequenceEqual(x.Properties, y.Properties);
        }

        public int GetHashCode(SettingsModel obj)
        {
            unchecked
            {
                var hashCode = obj.IsValid.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(obj.Name);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(obj.FullTypeName);
                hashCode = (hashCode * 397) ^ (obj.SectionPath is null ? 0 : StringComparer.Ordinal.GetHashCode(obj.SectionPath));
                hashCode = (hashCode * 397) ^ obj.CanConfigureOptions.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(obj.ProviderTypeName);
                foreach (var property in obj.Properties)
                {
                    hashCode = (hashCode * 397) ^ GetPropertyHashCode(property);
                }

                return hashCode;
            }
        }

        private static bool SequenceEqual(
            ImmutableArray<SettingsPropertyModel> left,
            ImmutableArray<SettingsPropertyModel> right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (!PropertyEquals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PropertyEquals(SettingsPropertyModel left, SettingsPropertyModel right)
        {
            return string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
                string.Equals(left.ConfigurationKey, right.ConfigurationKey, StringComparison.Ordinal) &&
                string.Equals(left.TypeName, right.TypeName, StringComparison.Ordinal) &&
                left.Kind == right.Kind &&
                left.IsRequired == right.IsRequired &&
                left.IsNullable == right.IsNullable &&
                left.IsSecret == right.IsSecret &&
                string.Equals(left.DefaultValue, right.DefaultValue, StringComparison.Ordinal) &&
                NullablePropertyEquals(left.Item, right.Item) &&
                SequenceEqual(left.Properties, right.Properties);
        }

        private static bool NullablePropertyEquals(SettingsPropertyModel? left, SettingsPropertyModel? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return PropertyEquals(left, right);
        }

        private static int GetPropertyHashCode(SettingsPropertyModel property)
        {
            unchecked
            {
                var hashCode = StringComparer.Ordinal.GetHashCode(property.Name);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(property.ConfigurationKey);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(property.TypeName);
                hashCode = (hashCode * 397) ^ property.Kind.GetHashCode();
                hashCode = (hashCode * 397) ^ property.IsRequired.GetHashCode();
                hashCode = (hashCode * 397) ^ property.IsNullable.GetHashCode();
                hashCode = (hashCode * 397) ^ property.IsSecret.GetHashCode();
                hashCode = (hashCode * 397) ^ (property.DefaultValue is null ? 0 : StringComparer.Ordinal.GetHashCode(property.DefaultValue));
                hashCode = (hashCode * 397) ^ (property.Item is null ? 0 : GetPropertyHashCode(property.Item));
                foreach (var child in property.Properties)
                {
                    hashCode = (hashCode * 397) ^ GetPropertyHashCode(child);
                }

                return hashCode;
            }
        }
    }

    private sealed class SettingsModel
    {
        private SettingsModel(
            bool isValid,
            string name,
            string fullTypeName,
            string? sectionPath,
            bool canConfigureOptions,
            string providerTypeName,
            ImmutableArray<SettingsPropertyModel> properties,
            DiagnosticDescriptor diagnostic,
            Location? location)
        {
            IsValid = isValid;
            Name = name;
            FullTypeName = fullTypeName;
            SectionPath = sectionPath;
            CanConfigureOptions = canConfigureOptions;
            ProviderTypeName = providerTypeName;
            Properties = properties;
            Diagnostic = diagnostic;
            Location = ValiantDiagnosticLocation.Create(location);
        }

        public bool IsValid { get; }

        public string Name { get; }

        public string FullTypeName { get; }

        public string? SectionPath { get; }

        public bool CanConfigureOptions { get; }

        public string ProviderTypeName { get; }

        public ImmutableArray<SettingsPropertyModel> Properties { get; }

        public DiagnosticDescriptor Diagnostic { get; }

        public ValiantDiagnosticLocation Location { get; }

        public static SettingsModel Valid(
            string name,
            string fullTypeName,
            string? sectionPath,
            bool canConfigureOptions,
            string providerTypeName,
            ImmutableArray<SettingsPropertyModel> properties)
        {
            return new SettingsModel(true, name, fullTypeName, sectionPath, canConfigureOptions, providerTypeName, properties, default!, null);
        }

        public static SettingsModel Invalid(DiagnosticDescriptor diagnostic, string name, string fullTypeName, Location? location)
        {
            return new SettingsModel(false, name, fullTypeName, null, false, string.Empty, ImmutableArray<SettingsPropertyModel>.Empty, diagnostic, location);
        }
    }

    private sealed class SettingsPropertyModel(
        string name,
        string configurationKey,
        string typeName,
        SettingsPropertyKind kind,
        bool isRequired,
        bool isNullable,
        bool isSecret,
        string? defaultValue,
        SettingsPropertyModel? item,
        ImmutableArray<SettingsPropertyModel> properties)
    {
        public string Name { get; } = name;

        public string ConfigurationKey { get; } = configurationKey;

        public string TypeName { get; } = typeName;

        public SettingsPropertyKind Kind { get; } = kind;

        public bool IsRequired { get; } = isRequired;

        public bool IsNullable { get; } = isNullable;

        public bool IsSecret { get; } = isSecret;

        public string? DefaultValue { get; } = defaultValue;

        public SettingsPropertyModel? Item { get; } = item;

        public ImmutableArray<SettingsPropertyModel> Properties { get; } = properties;
    }

    private enum SettingsPropertyKind
    {
        Unknown,
        String,
        Boolean,
        Integer,
        Number,
        Enum,
        Object,
        Array
    }
}
