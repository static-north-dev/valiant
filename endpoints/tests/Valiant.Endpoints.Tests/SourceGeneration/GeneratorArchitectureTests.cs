namespace Valiant.Endpoints.Tests.SourceGeneration;

public sealed class GeneratorArchitectureTests
{
    [Test]
    public async Task Endpoint_Generator_Uses_Only_Attribute_Scoped_Discovery()
    {
        var source = ReadRepositoryFile("endpoints/src/Valiant.Endpoints.AspNetCore.SourceGeneration/ValiantEndpointGenerator.cs");

        await Assert.That(source.Contains("ForAttributeWithMetadataName", StringComparison.Ordinal)).IsTrue();
        await Assert.That(CountOccurrences(source, "CreateSyntaxProvider") == 1).IsTrue();
        await Assert.That(source.Contains("IsPotentialQueryBindingOptIn", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("Endpoints.Registrations", StringComparison.Ordinal)).IsTrue();
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    [Test]
    public async Task Missing_Marker_Analyzer_Prefilters_Before_Semantic_Work()
    {
        var source = ReadRepositoryFile("endpoints/src/Valiant.Endpoints.AspNetCore.SourceGeneration/ValiantEndpointDeclarationAnalyzer.cs");

        await Assert.That(source.Contains("identifier is not (\"IEndpoint\" or \"IEndpointGroup\")", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.IndexOf("GetRightmostIdentifier", StringComparison.Ordinal))
            .IsLessThan(source.IndexOf("GetTypeInfo", StringComparison.Ordinal));
        await Assert.That(source.Contains("context.EnableConcurrentExecution()", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("GeneratedCodeAnalysisFlags.None", StringComparison.Ordinal)).IsTrue();
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
}
