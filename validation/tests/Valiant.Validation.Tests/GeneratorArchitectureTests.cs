namespace Valiant.Validation.Tests;

public sealed class GeneratorArchitectureTests
{
    [Test]
    public async Task Validation_Generator_Uses_Narrow_Incremental_Providers()
    {
        var source = ReadRepositoryFile("validation/src/Valiant.Validation.SourceGeneration/ValiantGenerator.cs") +
            ReadRepositoryFile("validation/src/Valiant.Validation.SourceGeneration/ValiantGenerator.AttributeParsing.cs");

        await Assert.That(source.Contains("ForAttributeWithMetadataName", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("\"Valiant.Validation.ValiantValidatorAttribute\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("\"Valiant.Validation.ValiantValidatorAttribute`1\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("ClassDeclarationSyntax { AttributeLists.Count: > 0 }", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("CreateValiantTypeProvider", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("CreateSyntaxProvider", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("CreateDependencyInjectionPropertyValidatorModel(validatorSymbol)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("ValiantPropertyValidatorDefinitionMetadataName", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Validation_Generator_Keeps_Compilation_Provider_Limited_To_Di_Feature_Gate()
    {
        var source = ReadRepositoryFile("validation/src/Valiant.Validation.SourceGeneration/ValiantGenerator.cs");

        await Assert.That(CountOccurrences(source, "CompilationProvider")).IsEqualTo(1);
        await Assert.That(source.Contains(".Select(static (compilation, _) => ValiantDependencyInjectionSymbols.HasDependencyInjection(compilation))", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("CompilationProvider.Combine", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Validation_Integration_Packages_Forward_Core_Build_And_Analyzer_Assets()
    {
        var projects = new[]
        {
            "validation/src/Valiant.Validation.AspNetCore/Valiant.Validation.AspNetCore.csproj",
            "validation/src/Valiant.Validation.AspNetCore.OpenApi/Valiant.Validation.AspNetCore.OpenApi.csproj",
            "validation/src/Valiant.Validation.Mediator/Valiant.Validation.Mediator.csproj",
            "validation/src/Valiant.Validation.MediatR/Valiant.Validation.MediatR.csproj"
        };

        foreach (var project in projects)
        {
            var source = ReadRepositoryFile(project);
            await Assert.That(source).Contains("Valiant.Validation.csproj\" PrivateAssets=\"contentfiles\"");
        }
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Valiant.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate repository root.");
        }

        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
