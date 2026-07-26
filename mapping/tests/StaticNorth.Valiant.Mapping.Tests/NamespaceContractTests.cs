using System.Reflection;
using StaticNorth.Valiant.Mapping;
using StaticNorth.Valiant.Mapping.SourceGeneration;

namespace StaticNorth.Valiant.Mapping.Tests;

public sealed class NamespaceContractTests
{
    [Test]
    public async Task Published_Mapping_Assemblies_Expose_Only_StaticNorth_Valiant_Namespaces()
    {
        Assembly[] assemblies = [
            typeof(MapperDefinition<,>).Assembly,
            typeof(ValiantMappingGenerator).Assembly
        ];

        foreach (var assembly in assemblies)
        {
            var unexpectedNamespaces = assembly
                .GetExportedTypes()
                .Select(static type => type.Namespace)
                .Where(static @namespace => @namespace is null || !@namespace.StartsWith("StaticNorth.Valiant.", StringComparison.Ordinal))
                .ToArray();

            await Assert.That(unexpectedNamespaces).IsEmpty();
        }
    }
}
