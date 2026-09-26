using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StaticNorth.Valiant.Settings.AspNetCore;
using StaticNorth.Valiant.Settings.Generated;

namespace StaticNorth.Valiant.Settings.Tests;

public sealed class SettingsSecurityTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Masked_Getters_And_Unmodeled_Objects_Cannot_Expose_Secrets(bool failGetter)
    {
        var settings = new SettingsSecurityModel
        {
            FailGetter = failGetter,
            Child = new SettingsSecurityModel { Child = new SettingsSecurityModel() },
            Opaque = new { Credential = "opaque-sensitive-value" }
        };
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(settings));
        using var serviceProvider = services.BuildServiceProvider();
#pragma warning disable CS0436 // The registry is intentionally generated independently in each assembly.
        var document = ValiantSettingsSchemaDocumentFactory.Create(
            [ValiantSettingsSchemas.Get<SettingsSecurityModel>()], serviceProvider, includeRuntimeValues: true);
#pragma warning restore CS0436

        await Assert.That(document.Schemas[0].RuntimeValuesError).IsNull();
        var json = JsonSerializer.Serialize(document);
        await Assert.That(json).DoesNotContain("sensitive-test-value");
        await Assert.That(json).DoesNotContain("opaque-sensitive-value");
        await Assert.That(json).Contains("********");
    }
}
