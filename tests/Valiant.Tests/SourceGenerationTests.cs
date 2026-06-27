using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Valiant.SourceGeneration;

namespace Valiant.Tests;

public sealed class SourceGenerationTests
{
    [Test]
    public async Task Marker_Attribute_With_Interface_Generates_Validator()
    {
        var result = RunGenerator(
            """
            using Valiant;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static ValidationContext<Person> Configure(ValidationContext<Person> context)
                {
                    context.RuleFor(x => x.Name).NotEmpty();
                    return context;
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Generic_Attribute_Compatibility_Form_Generates_Validator()
    {
        var result = RunGenerator(
            """
            using Valiant;

            [ValiantValidator<Person>]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static ValidationContext<Person> Configure(ValidationContext<Person> context)
                {
                    context.RuleFor(x => x.Name).NotEmpty();
                    return context;
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Typeof_Attribute_Compatibility_Form_Generates_Validator()
    {
        var result = RunGenerator(
            """
            using Valiant;

            [ValiantValidator(typeof(Person))]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static ValidationContext<Person> Configure(ValidationContext<Person> context)
                {
                    context.RuleFor(x => x.Name).NotEmpty();
                    return context;
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Missing_Partial_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant;

            [ValiantValidator]
            public sealed class PersonValidator : IValidatorDefinition<Person>
            {
                public static ValidationContext<Person> Configure(ValidationContext<Person> context)
                {
                    context.RuleFor(x => x.Name).NotEmpty();
                    return context;
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT001");
    }

    [Test]
    public async Task Missing_Definition_Interface_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant;

            [ValiantValidator]
            public sealed partial class PersonValidator
            {
                public static ValidationContext<Person> Configure(ValidationContext<Person> context)
                {
                    context.RuleFor(x => x.Name).NotEmpty();
                    return context;
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT005");
    }

    [Test]
    public async Task Attribute_And_Interface_Model_Mismatch_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant;

            [ValiantValidator(typeof(OtherPerson))]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static ValidationContext<Person> Configure(ValidationContext<Person> context)
                {
                    context.RuleFor(x => x.Name).NotEmpty();
                    return context;
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }

            public sealed class OtherPerson
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT006");
    }

    [Test]
    public async Task Missing_Configure_Method_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT003");
    }

    [Test]
    public async Task Non_Public_Configure_Method_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                static ValidationContext<Person> Configure(ValidationContext<Person> context)
                {
                    context.RuleFor(x => x.Name).NotEmpty();
                    return context;
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT004");
    }

    [Test]
    public async Task Unsupported_Rule_Syntax_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static ValidationContext<Person> Configure(ValidationContext<Person> context)
                {
                    _ = context;
                    return context;
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT004");
    }

    [Test]
    public async Task If_Else_Conditions_Generate_Runtime_Branches()
    {
        var result = RunGenerator(
            """
            using Valiant;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static ValidationContext<Person> Configure(ValidationContext<Person> context)
                {
                    if (context.Instance.RequiresName)
                    {
                        context.RuleFor(x => x.Name).NotEmpty();
                    }
                    else
                    {
                        context.RuleFor(x => x.Age).GreaterThanOrEqualTo(18);
                    }

                    return context;
                }
            }

            public sealed class Person
            {
                public bool RequiresName { get; init; }

                public string? Name { get; init; }

                public int Age { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated.Contains("if (instance.RequiresName)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("else", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Switch_Conditions_Generate_Runtime_Switches()
    {
        var result = RunGenerator(
            """
            using Valiant;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static ValidationContext<Person> Configure(ValidationContext<Person> context)
                {
                    switch (context.Instance.Kind)
                    {
                        case PersonKind.Child:
                            context.RuleFor(x => x.Age).LessThan(18);
                            break;
                        case PersonKind.Adult when context.Instance.RequiresName:
                            context.RuleFor(x => x.Name).NotEmpty();
                            break;
                        default:
                            context.RuleFor(x => x.Age).GreaterThanOrEqualTo(0);
                            break;
                    }

                    return context;
                }
            }

            public enum PersonKind
            {
                Child,
                Adult
            }

            public sealed class Person
            {
                public PersonKind Kind { get; init; }

                public bool RequiresName { get; init; }

                public string? Name { get; init; }

                public int Age { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated.Contains("switch (instance.Kind)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("case PersonKind.Adult when instance.RequiresName:", StringComparison.Ordinal)).IsTrue();
    }

    private static SourceGenerationResult RunGenerator(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            parseOptions,
            path: "/tests/GeneratedInput.cs");

        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(IValidator<>).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "Valiant.SourceGeneration.Tests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new ValiantGenerator())
            .WithUpdatedParseOptions(parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        var diagnostics = outputCompilation.GetDiagnostics()
            .Concat(generatorDiagnostics)
            .Where(static diagnostic => diagnostic.Id.StartsWith("VLT", StringComparison.Ordinal))
            .ToImmutableArray();

        var runResult = driver.GetRunResult().Results.Single();
        return new SourceGenerationResult(
            diagnostics,
            runResult.GeneratedSources.IsDefault ? ImmutableArray<GeneratedSourceResult>.Empty : runResult.GeneratedSources);
    }

    private static ImmutableArray<MetadataReference> GetTrustedPlatformReferences()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES was not available.");
        }

        return [
            ..trustedPlatformAssemblies
                .Split(Path.PathSeparator)
                .Select(static path => MetadataReference.CreateFromFile(path))
        ];
    }

    private sealed record SourceGenerationResult(
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableArray<GeneratedSourceResult> GeneratedSources);
}
