namespace Valiant.Mapping.Tests;

public sealed class GeneratorArchitectureTests
{
    [Test]
    public async Task Mapping_Generator_Uses_Attribute_Scoped_Incremental_Provider()
    {
        var source = ReadRepositoryFile("mapping/src/StaticNorth.Valiant.Mapping.SourceGeneration/ValiantMappingGenerator.cs");

        await Assert.That(source.Contains("ForAttributeWithMetadataName", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("MappingApi.Namespace + \".\" + MappingApi.MapperAttributeMetadataName", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("CreateSyntaxProvider", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("IsMapperCandidateSyntax", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Mapping_Generator_Keeps_Mapper_Call_Analysis_Narrowly_Scoped()
    {
        var source = ReadRepositoryFile("mapping/src/StaticNorth.Valiant.Mapping.SourceGeneration/ValiantMapperInvocationAnalyzer.cs");

        await Assert.That(source.Contains("GetPotentialInvocationKind", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("context.EnableConcurrentExecution()", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("GeneratedCodeAnalysisFlags.None", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("GetSymbolInfo(invocation", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Mapping_Generator_Does_Not_Keep_Superseded_Invocation_Analysis()
    {
        var generator = ReadRepositoryFile("mapping/src/StaticNorth.Valiant.Mapping.SourceGeneration/ValiantMappingGenerator.cs") +
            ReadRepositoryFile("mapping/src/StaticNorth.Valiant.Mapping.SourceGeneration/ValiantMappingGenerator.Emission.cs");

        await Assert.That(generator.Contains("CreateMapperFactoryInvocation", StringComparison.Ordinal)).IsFalse();
        await Assert.That(generator.Contains("CreateMapperFacadeInvocation", StringComparison.Ordinal)).IsFalse();
        await Assert.That(generator.Contains("ExecuteMapperFactoryInvocationDiagnostics", StringComparison.Ordinal)).IsFalse();
        await Assert.That(generator.Contains("ExecuteMapperFacadeInvocationDiagnostics", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Mapping_Generator_Keeps_Compilation_Provider_Limited_To_Feature_Metadata()
    {
        var source = ReadRepositoryFile("mapping/src/StaticNorth.Valiant.Mapping.SourceGeneration/ValiantMappingGenerator.cs");

        await Assert.That(CountOccurrences(source, "CompilationProvider")).IsEqualTo(1);
        await Assert.That(source.Contains("ValiantDependencyInjectionSymbols.HasDependencyInjection(compilation)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("CreateReferencedMapperPairRegistrations(compilation)", StringComparison.Ordinal)).IsFalse();
        await Assert.That(source.Contains("RegisterSourceOutput(mappers", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("CompilationProvider.Combine", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Mapping_Generator_Resolves_Flattening_On_Demand()
    {
        var source = ReadRepositoryFile("mapping/src/StaticNorth.Valiant.Mapping.SourceGeneration/ValiantMappingGenerator.cs");

        await Assert.That(source.Contains("TryGetFlattenedSourceMember", StringComparison.Ordinal)).IsTrue();
        await Assert.That(source.Contains("AddFlattenedSourceMembers", StringComparison.Ordinal)).IsFalse();
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

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StaticNorth.Valiant.sln")))
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
