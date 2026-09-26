using System.ComponentModel;
using System.Reflection;
using StaticNorth.Valiant.Validation;
using StaticNorth.Valiant.Validation.AspNetCore;
using StaticNorth.Valiant.Validation.AspNetCore.OpenApi;
using StaticNorth.Valiant.Validation.SourceGeneration;
using StaticNorth.Valiant.Validation.Infrastructure;

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

    [Test]
    public async Task Generated_Validator_Invocation_Bridge_Is_Hidden_From_IntelliSense()
    {
        Type[] infrastructureTypes =
        [
            typeof(IValiantValidatorInvoker),
            typeof(ValiantValidatorInvoker<>),
            typeof(ValidationResultBuilder),
            typeof(ValidationResultAccumulator),
            typeof(ValidationResultCache)
        ];

        foreach (var type in infrastructureTypes)
        {
            var attribute = type.GetCustomAttribute<EditorBrowsableAttribute>();

            await Assert.That(attribute).IsNotNull();
            await Assert.That(attribute!.State).IsEqualTo(EditorBrowsableState.Never);
        }
    }
}
