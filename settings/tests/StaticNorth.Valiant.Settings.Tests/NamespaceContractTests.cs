using System.Reflection;
using StaticNorth.Valiant.Settings;
using StaticNorth.Valiant.Settings.AspNetCore;
using StaticNorth.Valiant.Settings.SourceGeneration;
using StaticNorth.Valiant.Settings.Validation;

namespace StaticNorth.Valiant.Settings.Tests;

public sealed class NamespaceContractTests
{
    [Test]
    public async Task Published_Settings_Assemblies_Expose_Only_StaticNorth_Valiant_Namespaces()
    {
        Assembly[] assemblies = [
            typeof(ValiantSettingsAttribute).Assembly,
            typeof(ValiantSettingsUiOptions).Assembly,
            typeof(ValiantSettingsValidationOptions).Assembly,
            typeof(ValiantSettingsGenerator).Assembly
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
