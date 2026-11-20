using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StaticNorth.Valiant.Validation;

namespace StaticNorth.Valiant.Settings.Tests;

public sealed class SettingsValidationModeParityTests
{
    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    public async Task Generated_Registration_Binds_Options_And_Translates_Only_Errors(ValiantValidationMode mode)
    {
        await using var valid = CreateProvider(mode, "catalog", "3");
        var options = valid.GetRequiredService<IOptions<ModeParitySettings>>().Value;
        await Assert.That((options.Name, options.Retries)).IsEqualTo(("catalog", 3));

        await using var invalid = CreateProvider(mode, "", "0");
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            Task.FromResult(invalid.GetRequiredService<IOptions<ModeParitySettings>>().Value));
        await Assert.That(exception!.OptionsType).IsEqualTo(typeof(ModeParitySettings));
        await Assert.That(exception.Failures.SequenceEqual([
            "Name: A name is required.", "Retries: Retries must be positive."])).IsTrue();
    }

    private static ServiceProvider CreateProvider(ValiantValidationMode mode, string name, string retries)
    {
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Parity:Name"] = name, ["Parity:Retries"] = retries }).Build();

        var services = new ServiceCollection();
        // The generated extension types also exist in the referenced sample assemblies.
#pragma warning disable CS0436
        StaticNorth.Valiant.Validation.DependencyInjection.ValiantServiceCollectionExtensions.AddValiantValidators(services, options => options.Mode = mode);
        DependencyInjection.ValiantSettingsServiceCollectionExtensions.AddValiantSettings(services, configuration, options =>
        {
            options.ValidateWithValiant = true;
            options.RequireValidators = true;
        });
#pragma warning restore CS0436
        return services.BuildServiceProvider();
    }
}
