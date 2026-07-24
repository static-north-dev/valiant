using Valiant.Settings.DependencyInjection;

namespace Valiant.Settings.AspNetCore.Samples.Basic;

/// <summary>
/// Demonstrates the settings explorer and schema endpoint at the default path.
/// </summary>
public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddValiantSettings();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/", () => Results.Redirect(ValiantSettingsUiOptions.DefaultPath));
            endpoints.MapValiantSettingsUi();
        });
    }
}
