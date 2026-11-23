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
    private const int MaxSchemaNestingDepth = 32;
    private const int MaxSchemaProperties = 16_384;
    private const string SettingsAttributeMetadataName = "StaticNorth.Valiant.Settings.ValiantSettingsAttribute";
    private const string SecretAttributeMetadataName = "StaticNorth.Valiant.Settings.ValiantSecretAttribute";
    private const string ConfigurationKeyNameAttribute = "Microsoft.Extensions.Configuration.ConfigurationKeyNameAttribute";
    private const string ConfigurationIgnoreAttribute = "Microsoft.Extensions.Configuration.ConfigurationIgnoreAttribute";

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
        "Settings model '{0}' and its containing types must be non-generic because Valiant generates concrete schema providers",
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

    private static readonly DiagnosticDescriptor InaccessibleSettingsModel = new(
        "VLS003",
        "Settings model must be accessible to its generated schema provider",
        "Settings model '{0}' must be accessible from another source file in the same assembly",
        "StaticNorth.Valiant.Settings",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedOptionsModel = new(
        "VLS004",
        "Settings model cannot be bound through Options",
        "Settings model '{0}' must be a non-abstract class with a public parameterless constructor to use configure: true",
        "StaticNorth.Valiant.Settings",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor SettingsSchemaTooLarge = new(
        "VLS005",
        "Settings schema exceeds the property limit",
        "Settings model '{0}' expands beyond 16384 schema properties. Split the model into smaller settings sections.",
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
            .Select(static (model, _) => model!)
            .WithComparer(SettingsModelComparer.Instance);

        var validSettingsModels = settingsModels
            .Where(static model => model.IsValid)
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
        var assemblyName = context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName ?? string.Empty);
        var settingsRegistrationFeatures = context.CompilationProvider.Select(static (compilation, _) => GetSettingsRegistrationFeatures(compilation));

        context.RegisterSourceOutput(settingsModels, static (context, model) => ExecuteDiagnostics(context, model));
        context.RegisterSourceOutput(validSettingsModels, static (context, model) => ExecuteSchemaProvider(context, model));
        context.RegisterSourceOutput(assemblyName.Combine(schemaProviders),
            static (context, source) => ExecuteSchemaRegistry(context, source.Left, source.Right));
        context.RegisterSourceOutput(
            assemblyName.Combine(settingsRegistrationFeatures).Combine(settingsRegistrations),
            static (context, source) => ExecuteSettingsRegistration(context, source.Left.Right, source.Left.Left, source.Right));
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
        if (settingsType.IsGenericType)
        {
            return SettingsModel.Invalid(
                GenericSettingsModelNotSupported,
                settingsType.Name,
                fullTypeName,
                typeSyntax.Identifier.GetLocation());
        }

        var compilation = context.SemanticModel.Compilation;
        if (!compilation.IsSymbolAccessibleWithin(settingsType, compilation.Assembly) || HasFileLocalType(settingsType))
        {
            return SettingsModel.Invalid(
                InaccessibleSettingsModel,
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

        if (attribute.Configure && (settingsType.TypeKind != TypeKind.Class || settingsType.IsAbstract ||
            !settingsType.InstanceConstructors.Any(static ctor => ctor.DeclaredAccessibility == Accessibility.Public && ctor.Parameters.IsEmpty)))
        {
            return SettingsModel.Invalid(UnsupportedOptionsModel, settingsType.Name, fullTypeName, typeSyntax.Identifier.GetLocation());
        }

        var expansion = new SchemaExpansion();
        var properties = CreatePropertyModels(settingsType, compilation, expansion, includeDefaults: true, cancellationToken);
        if (expansion.Exceeded)
        {
            return SettingsModel.Invalid(SettingsSchemaTooLarge, settingsType.Name, fullTypeName, typeSyntax.Identifier.GetLocation());
        }

        return SettingsModel.Valid(
            settingsType.Name,
            fullTypeName,
            attribute.SectionPath,
            attribute.Configure,
            CreateProviderTypeName(fullTypeName),
            properties);
    }

    private static bool HasFileLocalType(INamedTypeSymbol settingsType)
    {
        for (var type = settingsType; type is not null; type = type.ContainingType)
        {
            if (type.IsFileLocal)
            {
                return true;
            }
        }

        return false;
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
        var propertyArrays = AppendPropertyArrayFields(builder, model.Properties);
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
        AppendPropertyArrayReference(builder, model.Properties, propertyArrays);
        builder.AppendLine(");");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        context.AddSource(model.ProviderTypeName + ".ValiantSettingsSchema.g.cs", builder.ToString());
    }

    private static void ExecuteSchemaRegistry(
        SourceProductionContext context,
        string assemblyName,
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
        var assemblyNamespace = ValiantSourceBuilder.AppendAssemblyNamespace(new StringBuilder(), assemblyName).ToString();
        builder.Append("namespace ").AppendLine(assemblyNamespace);
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Provides access to source-generated Valiant settings schemas in the current compilation.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    public static class SettingsSchemas");
        builder.AppendLine("    {");
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Gets all generated Valiant settings schema providers in the current compilation.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::StaticNorth.Valiant.Settings.IValiantSettingsSchemaProvider> All { get; } =");
        builder.AppendLine("            global::System.Array.AsReadOnly(new global::StaticNorth.Valiant.Settings.IValiantSettingsSchemaProvider[]");
        builder.AppendLine("            {");
        foreach (var model in orderedModels)
        {
            builder.Append("                new global::StaticNorth.Valiant.Settings.Generated.");
            builder.Append(model.ProviderTypeName);
            builder.AppendLine("(),");
        }

        builder.AppendLine("            });");
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

        builder.AppendLine();
        builder.AppendLine("namespace StaticNorth.Valiant.Settings");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>Provides access to schemas generated in this assembly.</summary>");
        builder.AppendLine("    public static class ValiantSettingsSchemas");
        builder.AppendLine("    {");
        builder.AppendLine("        /// <summary>Gets all generated schema providers in this assembly.</summary>");
        builder.Append("        public static global::System.Collections.Generic.IReadOnlyList<global::StaticNorth.Valiant.Settings.IValiantSettingsSchemaProvider> All => global::")
            .Append(assemblyNamespace).AppendLine(".SettingsSchemas.All;");
        builder.AppendLine("        /// <summary>Gets the generated schema provider for the specified settings model type.</summary>");
        builder.Append("        public static global::StaticNorth.Valiant.Settings.IValiantSettingsSchemaProvider Get<TSettings>() => global::")
            .Append(assemblyNamespace).AppendLine(".SettingsSchemas.Get<TSettings>();");
        builder.AppendLine("        /// <summary>Tries to get the generated schema provider for the specified settings model type.</summary>");
        builder.Append("        public static bool TryGet<TSettings>(out global::StaticNorth.Valiant.Settings.IValiantSettingsSchemaProvider? provider) => global::")
            .Append(assemblyNamespace).AppendLine(".SettingsSchemas.TryGet<TSettings>(out provider);");
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
        string assemblyName,
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
        builder.AppendLine("    internal static class ValiantSettingsServiceCollectionExtensions");
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
        builder.AppendLine("            _ = services ?? throw new global::System.ArgumentNullException(nameof(services));");
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
            builder.AppendLine("            _ = services ?? throw new global::System.ArgumentNullException(nameof(services));");
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
            builder.AppendLine("            _ = services ?? throw new global::System.ArgumentNullException(nameof(services));");
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

        const string configureSettings = ", global::System.Action<global::StaticNorth.Valiant.Settings.ValiantSettingsOptions>? configure";
        const string configuration = ", global::Microsoft.Extensions.Configuration.IConfiguration configuration";
        var configureBinding = features.HasValidationIntegration
            ? ", global::System.Action<global::StaticNorth.Valiant.Settings.Validation.ValiantSettingsValidationOptions>? configure"
            : configureSettings;
        ValiantSourceBuilder.AppendAssemblyRegistration(builder, assemblyName, "Settings",
            "global::StaticNorth.Valiant.Settings.DependencyInjection.ValiantSettingsServiceCollectionExtensions", "AddValiantSettings",
            (string.Empty, string.Empty),
            (configureSettings, ", configure"),
            (configuration, ", configuration"),
            (configuration + configureBinding, ", configuration, configure"));
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
        Compilation compilation,
        SchemaExpansion expansion,
        bool includeDefaults,
        CancellationToken cancellationToken)
    {
        var depth = expansion.ActiveTypes.Count;
        if (expansion.Cache.TryGetValue(type, out var cached) &&
            cached.TryGetValue((depth, includeDefaults), out var subtree))
        {
            expansion.Spend(subtree.Cost);
            return subtree.Properties;
        }

        var startBudget = expansion.Remaining;
        var startTruncations = expansion.Truncations;
        var properties = ImmutableArray.CreateBuilder<SettingsPropertyModel>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (member is IPropertySymbol property && names.Add(property.Name) && IsSettingsProperty(property))
                {
                    if (!TryGetConfigurationKey(property, out var configurationKey))
                    {
                        continue;
                    }

                    if (!expansion.Spend(1))
                    {
                        return [];
                    }

                    var isSecret = HasSecretAttribute(property);
                    var includePropertyDefaults = includeDefaults && !isSecret &&
                        !SensitiveSettingsNames.IsSensitive(property.Name, configurationKey);
                    properties.Add(CreatePropertyModel(
                        property.Type,
                        property.Name,
                        configurationKey,
                        property.IsRequired,
                        isSecret,
                        includePropertyDefaults ? GetDeclaredDefault(property, compilation, cancellationToken) : null,
                        compilation,
                        expansion,
                        includePropertyDefaults,
                        cancellationToken));
                }
            }
        }

        var result = properties.ToImmutable();
        if (!expansion.Exceeded && startTruncations == expansion.Truncations)
        {
            if (cached is null)
            {
                cached = [];
                expansion.Cache.Add(type, cached);
            }

            cached.Add((depth, includeDefaults), (result, startBudget - expansion.Remaining));
        }

        return result;
    }

    private static bool IsSettingsProperty(IPropertySymbol property)
    {
        return property is { IsStatic: false, Parameters.Length: 0, GetMethod.DeclaredAccessibility: Accessibility.Public, DeclaredAccessibility: Accessibility.Public };
    }

    private static string? GetDeclaredDefault(
        IPropertySymbol property,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        foreach (var reference in property.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not PropertyDeclarationSyntax { Initializer.Value: { } initializer } ||
                !compilation.ContainsSyntaxTree(initializer.SyntaxTree))
            {
                continue;
            }

            var constant = compilation.GetSemanticModel(initializer.SyntaxTree).GetConstantValue(initializer, cancellationToken);
            if (constant.HasValue)
            {
                return constant.Value switch
                {
                    null => "null",
                    bool value => value ? "true" : "false",
                    IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
                    _ => constant.Value.ToString()
                };
            }
        }

        return null;
    }

    private static bool TryGetConfigurationKey(IPropertySymbol property, out string key)
    {
        key = property.Name;

        // ConfigurationBinder uses the base definition of a virtual property setter.
        while (property.SetMethod is { IsOverride: true } && property.OverriddenProperty is { } overridden)
        {
            property = overridden;
        }

        foreach (var attribute in property.GetAttributes())
        {
            switch (attribute.AttributeClass?.ToDisplayString())
            {
                case ConfigurationIgnoreAttribute:
                    return false;
                case ConfigurationKeyNameAttribute when attribute.ConstructorArguments.Length == 1 &&
                    attribute.ConstructorArguments[0].Value is string alias &&
                    !string.IsNullOrWhiteSpace(alias):
                    key = alias;
                    break;
            }
        }

        return true;
    }

    private static SettingsPropertyModel CreatePropertyModel(
        ITypeSymbol type,
        string name,
        string configurationKey,
        bool isExplicitlyRequired,
        bool isSecret,
        string? defaultValue,
        Compilation compilation,
        SchemaExpansion expansion,
        bool includeDefaults,
        CancellationToken cancellationToken)
    {
        var effectiveType = UnwrapNullable(type);
        var itemType = GetCollectionItemType(effectiveType);
        var kind = itemType is null ? GetPropertyKind(effectiveType) : SettingsPropertyKind.Array;
        SettingsPropertyModel? item = null;
        var nestedProperties = ImmutableArray<SettingsPropertyModel>.Empty;

        // Recursive generic types can produce a different constructed type on every level.
        if (itemType is not null || kind == SettingsPropertyKind.Object)
        {
            if (expansion.ActiveTypes.Count >= MaxSchemaNestingDepth || !expansion.ActiveTypes.Add(effectiveType))
            {
                expansion.Truncations++;
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (itemType is not null)
                {
                    if (expansion.Spend(1))
                    {
                        item = CreatePropertyModel(
                            itemType, "$item", "$item", false, false, null,
                            compilation, expansion, includeDefaults, cancellationToken);
                    }
                }
                else if (effectiveType is INamedTypeSymbol namedType)
                {
                    nestedProperties = CreatePropertyModels(namedType, compilation, expansion, includeDefaults, cancellationToken);
                }

                expansion.ActiveTypes.Remove(effectiveType);
            }
        }

        return new SettingsPropertyModel(
            name,
            configurationKey,
            effectiveType.ToDisplayString(SchemaTypeDisplayFormat),
            kind,
            isExplicitlyRequired,
            IsNullable(type),
            isSecret,
            defaultValue,
            item,
            nestedProperties);
    }

    private sealed class SchemaExpansion
    {
        public HashSet<ITypeSymbol> ActiveTypes { get; } = new(SymbolEqualityComparer.Default);
        public Dictionary<ITypeSymbol, Dictionary<(int Depth, bool Defaults), (ImmutableArray<SettingsPropertyModel> Properties, int Cost)>> Cache { get; }
            = new(SymbolEqualityComparer.IncludeNullability);
        public int Remaining { get; private set; } = MaxSchemaProperties;
        public int Truncations { get; set; }
        public bool Exceeded => Remaining < 0;

        public bool Spend(int count)
        {
            Remaining -= count;
            return !Exceeded;
        }
    }

    private static bool HasSecretAttribute(IPropertySymbol property)
    {
        for (var current = property; current is not null; current = current.OverriddenProperty)
        {
            foreach (var attribute in current.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == SecretAttributeMetadataName)
                {
                    return true;
                }
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

        if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            return namedType.TypeArguments[0];
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
            (!IsSystemType(namedType) ||
                (namedType.MetadataName == "KeyValuePair`2" &&
                    namedType.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")) &&
            (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct))
        {
            return SettingsPropertyKind.Object;
        }

        return SettingsPropertyKind.Unknown;
    }

    private static bool IsSystemType(INamedTypeSymbol type)
    {
        var containingNamespace = type.ContainingNamespace?.ToDisplayString();
        return containingNamespace is "System" ||
            containingNamespace?.StartsWith("System.", StringComparison.Ordinal) is true;
    }

    private static bool IsNullable(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
    }

    private static Dictionary<ImmutableArray<SettingsPropertyModel>, string> AppendPropertyArrayFields(
        StringBuilder builder, ImmutableArray<SettingsPropertyModel> properties)
    {
        var arrays = new Dictionary<ImmutableArray<SettingsPropertyModel>, string>();
        VisitArray(properties);
        return arrays;

        void VisitArray(ImmutableArray<SettingsPropertyModel> children)
        {
            if (children.IsEmpty || arrays.ContainsKey(children))
            {
                return;
            }

            foreach (var child in children)
            {
                for (var property = child; property is not null; property = property.Item)
                {
                    VisitArray(property.Properties);
                }
            }

            var name = "Properties" + arrays.Count.ToString(CultureInfo.InvariantCulture);
            arrays.Add(children, name);
            builder.Append("        private static readonly global::System.Collections.Generic.IReadOnlyList<global::StaticNorth.Valiant.Settings.ValiantSettingsPropertySchema> ")
                .Append(name).AppendLine(" = global::System.Array.AsReadOnly(new global::StaticNorth.Valiant.Settings.ValiantSettingsPropertySchema[]");
            builder.AppendLine("        {");
            foreach (var child in children)
            {
                builder.Append("            ");
                AppendPropertySchema(builder, child, "            ", arrays);
                builder.AppendLine(",");
            }

            builder.AppendLine("        });");
            builder.AppendLine();
        }
    }

    private static void AppendPropertyArrayReference(
        StringBuilder builder, ImmutableArray<SettingsPropertyModel> properties,
        Dictionary<ImmutableArray<SettingsPropertyModel>, string> arrays)
    {
        builder.Append(properties.IsEmpty
            ? "global::System.Array.Empty<global::StaticNorth.Valiant.Settings.ValiantSettingsPropertySchema>()"
            : arrays[properties]);
    }

    private static void AppendPropertySchema(
        StringBuilder builder,
        SettingsPropertyModel property,
        string indent,
        Dictionary<ImmutableArray<SettingsPropertyModel>, string> arrays)
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
            AppendPropertySchema(builder, property.Item, indent + "    ", arrays);
            builder.AppendLine(",");
        }

        builder.Append(indent);
        builder.Append("    ");
        AppendPropertyArrayReference(builder, property.Properties, arrays);
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
                Equals(x.Diagnostic, y.Diagnostic) &&
                x.Location.Equals(y.Location) &&
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
                hashCode = (hashCode * 397) ^ (obj.Diagnostic?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ obj.Location.GetHashCode();
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
