using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StaticNorth.Valiant.Settings.DependencyInjection;

namespace StaticNorth.Valiant.Settings.Samples.Basic;

/// <summary>
/// Demonstrates the normal configured-settings flow from registration to <see cref="IOptions{TOptions}"/>.
/// </summary>
public static class Sample
{
    public static void Run()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Samples:Basic:Message"] = "Hello from Valiant Settings",
                ["Samples:Db:ConnectionString"] = "Server=myServer;Database=myDB;User Id=myUser;Password=myPass;"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddValiantSettings(configuration);

        using var provider = services.BuildServiceProvider();
        var basicSettings = provider.GetRequiredService<IOptions<BasicSettings>>().Value;
        var dbSettings = provider.GetRequiredService<IOptions<DbSettings>>().Value;

        Console.WriteLine($"Basic Settings Message: '{basicSettings.Message}");
        Console.WriteLine($"DB Connection String: '{dbSettings.ConnectionString}'");
    }
}
