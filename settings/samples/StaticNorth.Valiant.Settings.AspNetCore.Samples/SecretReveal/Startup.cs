using StaticNorth.Valiant.Settings.DependencyInjection;

namespace StaticNorth.Valiant.Settings.AspNetCore.Samples.SecretReveal;

/// <summary>
/// Demonstrates explicit secret reveal through the revealSecrets query parameter.
/// </summary>
public sealed class Startup
{
    public const string Path = "/secret-reveal";

    public void ConfigureServices(IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Samples:AspNetCore:SecretReveal:Name"] = "catalog",
                ["Samples:AspNetCore:SecretReveal:ApiKey"] = "revealable-secret"
            })
            .Build();

        services.AddValiantSettings(configuration);
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
                options.AllowSecretReveal = true;
            });
        });
    }
}
