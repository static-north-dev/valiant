using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StaticNorth.Valiant.Settings.DependencyInjection;

namespace StaticNorth.Valiant.Settings.Samples.RegistrationOptions;

/// <summary>
/// Demonstrates default schema-provider registration and binding without schema providers.
/// </summary>
public static class Sample
{
    public static void Run()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Samples:Registration:Value"] = "bound without schema providers"
            })
            .Build();

        var defaultServices = new ServiceCollection();
        defaultServices.AddValiantSettings(configuration);
        using var defaultProvider = defaultServices.BuildServiceProvider();

        var bindingOnlyServices = new ServiceCollection();
        bindingOnlyServices.AddValiantSettings(configuration, options =>
        {
            options.RegisterSchemaProviders = false;
        });

        using var bindingOnlyProvider = bindingOnlyServices.BuildServiceProvider();
        var settings = bindingOnlyProvider
            .GetRequiredService<IOptions<RegistrationSettings>>()
            .Value;

        Console.WriteLine(
            $"Default schema providers: " +
            $"{defaultProvider.GetServices<IValiantSettingsSchemaProvider>().Count()}");
        Console.WriteLine(
            $"Schema providers after disabling registration: " +
            $"{bindingOnlyProvider.GetServices<IValiantSettingsSchemaProvider>().Count()}");
        Console.WriteLine($"Options still bind: {settings.Value}");
    }
}
