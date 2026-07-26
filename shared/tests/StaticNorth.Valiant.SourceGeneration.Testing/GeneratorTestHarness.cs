using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace StaticNorth.Valiant.SourceGeneration.Testing;

internal static class GeneratorTestHarness
{
    public static ImmutableArray<MetadataReference> GetTrustedPlatformReferences(string? excludedAssemblyFileName = null)
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES was not available.");
        }

        return [
            ..trustedPlatformAssemblies
                .Split(Path.PathSeparator)
                .Where(path => excludedAssemblyFileName is null || !string.Equals(
                    Path.GetFileName(path),
                    excludedAssemblyFileName,
                    StringComparison.OrdinalIgnoreCase))
                .Select(static path => MetadataReference.CreateFromFile(path))
        ];
    }

    public static CSharpCompilation CreateCompilation(
        string source,
        CSharpParseOptions parseOptions,
        ImmutableArray<MetadataReference> references,
        string assemblyName,
        string path = "/tests/GeneratedInput.cs")
    {
        return CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: path)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    public static GeneratorExecutionResult Run(
        IIncrementalGenerator generator,
        CSharpCompilation compilation,
        CSharpParseOptions parseOptions)
    {
        var driver = CSharpGeneratorDriver.Create(generator)
            .WithUpdatedParseOptions(parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var runResult = driver.GetRunResult().Results.Single();

        return new GeneratorExecutionResult(
            outputCompilation,
            generatorDiagnostics,
            runResult.GeneratedSources.IsDefault ? ImmutableArray<GeneratedSourceResult>.Empty : runResult.GeneratedSources);
    }

    public static ImmutableArray<IncrementalStepRunReason> RunTrackedGeneratorEdit(
        IIncrementalGenerator generator,
        string before,
        string after,
        string stepName,
        ImmutableArray<MetadataReference> references,
        string assemblyName)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var beforeCompilation = CreateCompilation(before, parseOptions, references, assemblyName);
        var afterCompilation = CreateCompilation(after, parseOptions, references, assemblyName);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            [],
            parseOptions,
            null,
            new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(beforeCompilation);
        driver = driver.RunGenerators(afterCompilation);

        return [.. driver.GetRunResult().Results.Single().TrackedSteps[stepName]
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)];
    }
}

internal sealed record GeneratorExecutionResult(
    Compilation OutputCompilation,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<GeneratedSourceResult> GeneratedSources);
