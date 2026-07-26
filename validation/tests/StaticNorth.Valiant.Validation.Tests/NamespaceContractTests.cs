using System.Reflection;
using StaticNorth.Valiant.Validation;
using StaticNorth.Valiant.Validation.AspNetCore;
using StaticNorth.Valiant.Validation.AspNetCore.OpenApi;
using StaticNorth.Valiant.Validation.SourceGeneration;

namespace StaticNorth.Valiant.Validation.Tests;

public sealed class NamespaceContractTests
{
    [Test]
    public async Task Published_Validation_Assemblies_Expose_Only_StaticNorth_Valiant_Namespaces()
    {
        Assembly[] assemblies = [
            typeof(ValidatorDefinition<>).Assembly,
            typeof(ValiantMinimalApiExtensions).Assembly,
            typeof(ValiantOpenApiExtensions).Assembly,
            typeof(StaticNorth.Valiant.Validation.MediatR.ValiantValidationBehavior<,>).Assembly,
            typeof(StaticNorth.Valiant.Validation.Mediator.ValiantValidationBehavior<,>).Assembly,
            typeof(ValiantGenerator).Assembly
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
