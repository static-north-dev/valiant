using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Valiant.Settings.AspNetCore;
using Valiant.Settings.Validation;
using Valiant.SourceGeneration.Testing;
using Valiant.Validation;
using Valiant.Settings.SourceGeneration;

namespace Valiant.Settings.Tests;

public sealed class SourceGenerationTests
{
    [Test]
    public async Task Trusted_Platform_References_Exclude_The_Executing_Test_Assembly()
    {
        var referenceFileNames = GetTrustedPlatformReferences()
            .Select(static reference => Path.GetFileName(reference.Display));

        await Assert.That(referenceFileNames).DoesNotContain(Path.GetFileName(typeof(SourceGenerationTests).Assembly.Location));
    }

    [Test]
    public async Task Settings_Model_Generates_Schema_Provider_With_Section_Path()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Settings;

            [ValiantSettings("ConnectionStrings:Primary")]
            public sealed record DbSettings
            {
                [ValiantSecret]
                public string ConnectionString { get; init; } = "";

                public int CommandTimeoutSeconds { get; init; }
            }
            """);

        var registry = assembly.GetType("Valiant.Settings.ValiantSettingsSchemas", throwOnError: true)!;
        var providers = (IReadOnlyList<IValiantSettingsSchemaProvider>)registry.GetProperty("All")!.GetValue(null)!;
        var provider = providers.Single();
        var schema = provider.Schema;

        await Assert.That(provider.SectionPath).IsEqualTo("ConnectionStrings:Primary");
        await Assert.That(schema.SectionPath).IsEqualTo("ConnectionStrings:Primary");
        await Assert.That(schema.Properties).Count().IsEqualTo(2);
        await Assert.That(schema.Properties[0].Name).IsEqualTo("ConnectionString");
        await Assert.That(schema.Properties[0].Kind).IsEqualTo(ValiantSettingsPropertyKind.String);
        await Assert.That(schema.Properties[0].IsRequired).IsTrue();
        await Assert.That(schema.Properties[0].IsSecret).IsTrue();
        await Assert.That(schema.Properties[1].Name).IsEqualTo("CommandTimeoutSeconds");
        await Assert.That(schema.Properties[1].Kind).IsEqualTo(ValiantSettingsPropertyKind.Integer);
    }

    [Test]
    public async Task Settings_Model_Generates_Nested_Object_Schema()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Settings;

            [ValiantSettings("Features:Search")]
            public sealed record SearchSettings
            {
                public bool Enabled { get; init; }

                public SearchIndexSettings Index { get; init; } = new();
            }

            public sealed record SearchIndexSettings
            {
                public string Name { get; init; } = "";
            }
            """);

        var registry = assembly.GetType("Valiant.Settings.ValiantSettingsSchemas", throwOnError: true)!;
        var provider = ((IReadOnlyList<IValiantSettingsSchemaProvider>)registry.GetProperty("All")!.GetValue(null)!).Single();
        var index = provider.Schema.Properties.Single(static property => property.Name == "Index");

        await Assert.That(index.Kind).IsEqualTo(ValiantSettingsPropertyKind.Object);
        await Assert.That(index.Properties).Count().IsEqualTo(1);
        await Assert.That(index.Properties[0].Name).IsEqualTo("Name");
        await Assert.That(index.Properties[0].Kind).IsEqualTo(ValiantSettingsPropertyKind.String);
    }

    [Test]
    public async Task Configured_Settings_Model_With_Section_Path_Generates_Options_Registration()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Settings;

            [ValiantSettings("ConnectionStrings:Primary", configure: true)]
            public sealed class DbSettings
            {
                public string? ConnectionString { get; set; }

                public int CommandTimeoutSeconds { get; set; }
            }
            """);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Primary:ConnectionString"] = "Server=localhost;Database=valiant;",
                ["ConnectionStrings:Primary:CommandTimeoutSeconds"] = "45"
            })
            .Build();
        var services = new ServiceCollection();
        var extensionType = assembly.GetType("Valiant.Settings.DependencyInjection.ValiantSettingsServiceCollectionExtensions", throwOnError: true)!;
        extensionType.GetMethod("AddValiantSettings", [typeof(IServiceCollection), typeof(IConfiguration)])!
            .Invoke(null, [services, configuration]);

        using var serviceProvider = services.BuildServiceProvider();
        var settingsType = assembly.GetType("DbSettings", throwOnError: true)!;
        var optionsType = typeof(IOptions<>).MakeGenericType(settingsType);
        var options = serviceProvider.GetRequiredService(optionsType);
        var value = optionsType.GetProperty(nameof(IOptions<object>.Value))!.GetValue(options)!;
        var schemaProviders = serviceProvider.GetServices<IValiantSettingsSchemaProvider>().ToArray();

        await Assert.That(settingsType.GetProperty("ConnectionString")!.GetValue(value)).IsEqualTo("Server=localhost;Database=valiant;");
        await Assert.That(settingsType.GetProperty("CommandTimeoutSeconds")!.GetValue(value)).IsEqualTo(45);
        await Assert.That(schemaProviders).Count().IsEqualTo(1);
        await Assert.That(schemaProviders[0].SectionPath).IsEqualTo("ConnectionStrings:Primary");
    }

    [Test]
    public async Task Settings_Model_With_Section_Path_Does_Not_Generate_Options_Registration_By_Default()
    {
        var result = RunGenerator(
            """
            using Valiant.Settings;

            [ValiantSettings("ConnectionStrings:Primary")]
            public sealed class DbSettings
            {
                public string? ConnectionString { get; set; }
            }
            """);

        var dependencyInjection = GetGeneratedSource(result, "Valiant.Settings.DependencyInjection.g.cs");

        await Assert.That(dependencyInjection).DoesNotContain("Configure<global::DbSettings>");
        await Assert.That(dependencyInjection).Contains("TryAddEnumerable");
    }

    [Test]
    public async Task AddValiantSettings_Can_Skip_Schema_Provider_Registration()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Settings;

            [ValiantSettings("ConnectionStrings:Primary", configure: true)]
            public sealed class DbSettings
            {
                public string? ConnectionString { get; set; }
            }
            """);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Primary:ConnectionString"] = "Server=localhost;Database=valiant;"
            })
            .Build();
        var services = new ServiceCollection();
        var extensionType = assembly.GetType("Valiant.Settings.DependencyInjection.ValiantSettingsServiceCollectionExtensions", throwOnError: true)!;
        extensionType.GetMethod(
                "AddValiantSettings",
                [typeof(IServiceCollection), typeof(IConfiguration), typeof(Action<ValiantSettingsValidationOptions>)])!
            .Invoke(
                null,
                [
                    services,
                    configuration,
                    new Action<ValiantSettingsValidationOptions>(options => options.RegisterSchemaProviders = false)
                ]);

        using var serviceProvider = services.BuildServiceProvider();
        var settingsType = assembly.GetType("DbSettings", throwOnError: true)!;
        var optionsType = typeof(IOptions<>).MakeGenericType(settingsType);
        var options = serviceProvider.GetRequiredService(optionsType);
        var value = optionsType.GetProperty(nameof(IOptions<object>.Value))!.GetValue(options)!;
        var schemaProviders = serviceProvider.GetServices<IValiantSettingsSchemaProvider>().ToArray();

        await Assert.That(settingsType.GetProperty("ConnectionString")!.GetValue(value)).IsEqualTo("Server=localhost;Database=valiant;");
        await Assert.That(schemaProviders).IsEmpty();
    }

    [Test]
    public async Task Settings_Model_Can_Register_Valiant_Options_Validation()
    {
        var assembly = CompileAndLoad(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using Valiant.Settings;
            using Valiant.Validation;

            [ValiantSettings("ConnectionStrings:Primary", configure: true)]
            public sealed class DbSettings
            {
                public string? ConnectionString { get; set; }
            }

            public sealed class DbSettingsValidator : IValidator<DbSettings>
            {
                public ValidationResult Validate(DbSettings instance)
                {
                    return string.IsNullOrWhiteSpace(instance.ConnectionString)
                        ? new ValidationResult([new ValidationFailure(nameof(DbSettings.ConnectionString), "Required", "Connection string is required.")])
                        : ValidationResult.Valid;
                }

                public ValueTask<ValidationResult> ValidateAsync(DbSettings instance, CancellationToken cancellationToken = default)
                {
                    return new ValueTask<ValidationResult>(Validate(instance));
                }

                public bool IsValid(DbSettings instance)
                {
                    return Validate(instance).IsValid;
                }

                public async ValueTask<bool> IsValidAsync(DbSettings instance, CancellationToken cancellationToken = default)
                {
                    return (await ValidateAsync(instance, cancellationToken)).IsValid;
                }
            }
            """);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Primary:ConnectionString"] = ""
            })
            .Build();
        var services = new ServiceCollection();
        var settingsType = assembly.GetType("DbSettings", throwOnError: true)!;
        var validatorType = assembly.GetType("DbSettingsValidator", throwOnError: true)!;
        var validatorServiceType = typeof(IValidator<>).MakeGenericType(settingsType);
        services.AddSingleton(validatorServiceType, validatorType);

        var extensionType = assembly.GetType("Valiant.Settings.DependencyInjection.ValiantSettingsServiceCollectionExtensions", throwOnError: true)!;
        extensionType.GetMethod(
                "AddValiantSettings",
                [typeof(IServiceCollection), typeof(IConfiguration), typeof(Action<ValiantSettingsValidationOptions>)])!
            .Invoke(
                null,
                [
                    services,
                    configuration,
                    new Action<ValiantSettingsValidationOptions>(options => options.ValidateWithValiant = true)
                ]);

        using var serviceProvider = services.BuildServiceProvider();
        var optionsValidatorType = typeof(IValidateOptions<>).MakeGenericType(settingsType);
        var optionsType = typeof(IOptions<>).MakeGenericType(settingsType);
        var options = serviceProvider.GetRequiredService(optionsType);
        var validators = serviceProvider.GetServices(optionsValidatorType);

        OptionsValidationException? exception = null;
        try
        {
            optionsType.GetProperty(nameof(IOptions<object>.Value))!.GetValue(options);
        }
        catch (System.Reflection.TargetInvocationException targetInvocationException)
            when (targetInvocationException.InnerException is OptionsValidationException optionsValidationException)
        {
            exception = optionsValidationException;
        }

        await Assert.That(validators).Count().IsEqualTo(1);
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Failures).Contains("ConnectionString: Connection string is required.");
    }

    [Test]
    public async Task Settings_Schema_Document_Factory_Creates_Ui_Friendly_Document()
    {
        var provider = new TestSettingsSchemaProvider(
            new ValiantSettingsSchema(
                typeof(TestSettings),
                nameof(TestSettings),
                "Tests:Settings",
                [
                    new ValiantSettingsPropertySchema(
                        "Name",
                        "Name",
                        "string",
                        ValiantSettingsPropertyKind.String,
                        isRequired: true,
                        isNullable: false,
                        isSecret: false)
                ]));

        var document = ValiantSettingsSchemaDocumentFactory.Create([provider]);

        await Assert.That(document.Schemas).Count().IsEqualTo(1);
        await Assert.That(document.Schemas[0].Name).IsEqualTo(nameof(TestSettings));
        await Assert.That(document.Schemas[0].SectionPath).IsEqualTo("Tests:Settings");
        await Assert.That(document.Schemas[0].Properties[0].Kind).IsEqualTo("String");
        await Assert.That(document.Schemas[0].Properties[0].IsRequired).IsTrue();
    }

    [Test]
    public async Task Settings_Schema_Document_Factory_Can_Include_Masked_Runtime_Values()
    {
        var provider = new TestSettingsSchemaProvider(
            new ValiantSettingsSchema(
                typeof(TestSettings),
                nameof(TestSettings),
                "Tests:Settings",
                [
                    new ValiantSettingsPropertySchema(
                        "Name",
                        "Name",
                        "string",
                        ValiantSettingsPropertyKind.String,
                        isRequired: true,
                        isNullable: false,
                        isSecret: false),
                    new ValiantSettingsPropertySchema(
                        "Password",
                        "Password",
                        "string",
                        ValiantSettingsPropertyKind.String,
                        isRequired: true,
                        isNullable: false,
                        isSecret: true)
                ]));
        var services = new ServiceCollection();
        services.Configure<TestSettings>(options =>
        {
            options.Name = "runtime";
            options.Password = "super-secret";
        });

        using var serviceProvider = services.BuildServiceProvider();
        var document = ValiantSettingsSchemaDocumentFactory.Create([provider], serviceProvider, includeRuntimeValues: true);
        var runtimeValues = (IReadOnlyDictionary<string, object?>)document.Schemas[0].RuntimeValues!;
        var revealedDocument = ValiantSettingsSchemaDocumentFactory.Create(
            [provider],
            serviceProvider,
            includeRuntimeValues: true,
            revealSecrets: true,
            canRevealSecrets: true);
        var revealedRuntimeValues = (IReadOnlyDictionary<string, object?>)revealedDocument.Schemas[0].RuntimeValues!;

        await Assert.That(runtimeValues["Name"]).IsEqualTo("runtime");
        await Assert.That(runtimeValues["Password"]).IsEqualTo("********");
        await Assert.That(document.Schemas[0].RuntimeValuesError).IsNull();
        await Assert.That(document.CanRevealSecrets).IsFalse();
        await Assert.That(revealedDocument.CanRevealSecrets).IsTrue();
        await Assert.That(revealedDocument.SecretsRevealed).IsTrue();
        await Assert.That(revealedRuntimeValues["Password"]).IsEqualTo("super-secret");
    }

    [Test]
    public async Task Settings_Ui_Maps_Root_And_Schema_Endpoints_Without_Duplicates()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        app.MapValiantSettingsUi();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        await Assert.That(endpoints).IsEquivalentTo(["/_valiant/settings/", "/_valiant/settings/schemas"]);
    }

    [Test]
    public async Task Settings_Ui_Allows_Custom_Route_Prefix()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        app.MapValiantSettingsUi(options => options.Path = "ops/settings/");

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        await Assert.That(endpoints).IsEquivalentTo(["/ops/settings/", "/ops/settings/schemas"]);
    }

    [Test]
    public async Task Unannotated_Types_Do_Not_Generate_Settings_Sources()
    {
        var result = RunGenerator(
            """
            public sealed record DbSettings
            {
                public string? ConnectionString { get; init; }
            }
            """);

        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    [Test]
    public async Task Aggregate_Settings_Sources_Stay_Stable_When_Only_Property_Schema_Changes()
    {
        var before = RunGenerator(
            """
            using Valiant.Settings;

            [ValiantSettings("ConnectionStrings:Primary", configure: true)]
            public sealed class DbSettings
            {
                public string? ConnectionString { get; set; }
            }
            """);
        var after = RunGenerator(
            """
            using Valiant.Settings;

            [ValiantSettings("ConnectionStrings:Primary", configure: true)]
            public sealed class DbSettings
            {
                public string? ConnectionString { get; set; }

                public int CommandTimeoutSeconds { get; set; }
            }
            """);

        var beforeRegistry = GetGeneratedSource(before, "ValiantSettingsSchemas.g.cs");
        var afterRegistry = GetGeneratedSource(after, "ValiantSettingsSchemas.g.cs");
        var beforeDependencyInjection = GetGeneratedSource(before, "Valiant.Settings.DependencyInjection.g.cs");
        var afterDependencyInjection = GetGeneratedSource(after, "Valiant.Settings.DependencyInjection.g.cs");
        var beforeProvider = before.GeneratedSources.Single(static source => source.HintName.EndsWith(".ValiantSettingsSchema.g.cs", StringComparison.Ordinal)).SourceText.ToString();
        var afterProvider = after.GeneratedSources.Single(static source => source.HintName.EndsWith(".ValiantSettingsSchema.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(beforeRegistry).IsEqualTo(afterRegistry);
        await Assert.That(beforeDependencyInjection).IsEqualTo(afterDependencyInjection);
        await Assert.That(beforeProvider).IsNotEqualTo(afterProvider);
    }

    [Test]
    public async Task Generic_Settings_Model_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Settings;

            [ValiantSettings("Generic")]
            public sealed record GenericSettings<T>
            {
                public T? Value { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Id == "VLS001" && diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
    }

    [Test]
    public async Task Empty_Settings_Section_Path_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Settings;

            [ValiantSettings("")]
            public sealed record DbSettings
            {
                public string? ConnectionString { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Any(static diagnostic => diagnostic.Id == "VLS002" && diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
    }

    [Test]
    public async Task Schema_Lookup_Uses_Linear_Search_For_Small_Sets_And_Dictionary_For_Large_Sets()
    {
        var small = GetGeneratedSource(RunGenerator(CreateSettingsSetSource(1)), "ValiantSettingsSchemas.g.cs");
        var large = GetGeneratedSource(RunGenerator(CreateSettingsSetSource(9)), "ValiantSettingsSchemas.g.cs");

        await Assert.That(small).DoesNotContain("ProvidersByType");
        await Assert.That(large).Contains("ProvidersByType.TryGetValue(typeof(TSettings), out provider)");
        await Assert.That(CSharpSyntaxTree.ParseText(large).GetDiagnostics()).IsEmpty();
    }

    [Test]
    public async Task Unrelated_Edit_Leaves_Tracked_Settings_Step_Cached_Or_Unchanged()
    {
        const string settings = """
            using Valiant.Settings;

            [ValiantSettings("Database")]
            public sealed class DatabaseSettings
            {
                public string? ConnectionString { get; init; }
            }
            """;

        var reasons = RunTrackedGeneratorEdit(
            settings + "\npublic sealed class Unrelated { public int Value { get; init; } }",
            settings + "\npublic sealed class Unrelated { public int Value { get; init; } public string? Text { get; init; } }",
            "Settings.Model");

        await Assert.That(reasons).IsNotEmpty();
        await Assert.That(reasons.All(static reason => reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged)).IsTrue();
    }

    private static string CreateSettingsSetSource(int count)
    {
        var builder = new System.Text.StringBuilder("using Valiant.Settings;\n");
        for (var index = 0; index < count; index++)
        {
            builder.Append("[ValiantSettings(\"Section")
                .Append(index)
                .Append("\")] public sealed class Settings")
                .Append(index)
                .AppendLine(" { public string? Value { get; init; } }");
        }

        return builder.ToString();
    }

    private static SourceGenerationResult RunGenerator(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(ValiantSettingsAttribute).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(ValiantSettingsValidationOptions).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(IValidator<>).Assembly.Location));

        var compilation = GeneratorTestHarness.CreateCompilation(
            source,
            parseOptions,
            references,
            "Valiant.Settings.SourceGeneration.Tests");
        var execution = GeneratorTestHarness.Run(new ValiantSettingsGenerator(), compilation, parseOptions);

        var diagnostics = execution.OutputCompilation.GetDiagnostics()
            .Concat(execution.GeneratorDiagnostics)
            .Where(static diagnostic => diagnostic.Id.StartsWith("VLS", StringComparison.Ordinal))
            .ToImmutableArray();

        return new SourceGenerationResult(
            diagnostics,
            execution.GeneratedSources);
    }

    private static ImmutableArray<IncrementalStepRunReason> RunTrackedGeneratorEdit(
        string before,
        string after,
        string stepName)
    {
        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(ValiantSettingsAttribute).Assembly.Location));
        return GeneratorTestHarness.RunTrackedGeneratorEdit(
            new ValiantSettingsGenerator(),
            before,
            after,
            stepName,
            references,
            "Valiant.Settings.Tracked.Tests");
    }

    private static string GetGeneratedSource(SourceGenerationResult result, string hintName)
    {
        return result.GeneratedSources
            .Single(source => string.Equals(source.HintName, hintName, StringComparison.Ordinal))
            .SourceText
            .ToString();
    }

    private static System.Reflection.Assembly CompileAndLoad(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            parseOptions,
            path: "/tests/GeneratedInput.cs");

        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(ValiantSettingsAttribute).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(ValiantSettingsValidationOptions).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(IValidator<>).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "Valiant.Settings.GeneratedSchema.Tests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new ValiantSettingsGenerator())
            .WithUpdatedParseOptions(parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        var diagnostics = outputCompilation.GetDiagnostics()
            .Concat(generatorDiagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        if (!diagnostics.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        using var stream = new MemoryStream();
        var emitResult = outputCompilation.Emit(stream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, emitResult.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        return System.Reflection.Assembly.Load(stream.ToArray());
    }

    private static ImmutableArray<MetadataReference> GetTrustedPlatformReferences()
    {
        return GeneratorTestHarness.GetTrustedPlatformReferences(Path.GetFileName(typeof(SourceGenerationTests).Assembly.Location));
    }

    private sealed record SourceGenerationResult(
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableArray<GeneratedSourceResult> GeneratedSources);

    private sealed class TestSettings
    {
        public string Name { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    private sealed class TestSettingsSchemaProvider(ValiantSettingsSchema schema) : IValiantSettingsSchemaProvider
    {
        public Type SettingsType => schema.SettingsType;

        public string? SectionPath => schema.SectionPath;

        public ValiantSettingsSchema Schema => schema;
    }
}
