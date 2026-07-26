using StaticNorth.Valiant.Settings.DependencyInjection;

namespace StaticNorth.Valiant.Settings.AspNetCore.Samples.RuntimeValues;

/// <summary>
/// Demonstrates runtime values with explicit and name-matched secrets masked.
/// </summary>
public sealed class Startup
{
    public const string Path = "/runtime-values";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddValiantSettings(CreateConfiguration());
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/", () => Results.Redirect(Path));
            endpoints.MapValiantSettingsUi(options =>
            {
                options.Path = Path;
                options.ShowRuntimeValues = true;
                options.AllowSecretReveal = false;
            });
        });
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Samples:AspNetCore:RuntimeValues:Name"] = "catalog",
                ["Samples:AspNetCore:RuntimeValues:ApiKey"] = "explicit-secret",
                ["Samples:AspNetCore:RuntimeValues:AccessToken"] = "name-matched-secret"
            })
            .Build();
    }
}
