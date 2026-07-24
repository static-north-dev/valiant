using Microsoft.AspNetCore.Authentication;
using Valiant.Settings.AspNetCore;
using Valiant.Settings.DependencyInjection;

namespace Valiant.Settings.AspNetCore.Samples.Configuration;

/// <summary>
/// Demonstrates a custom UI path and authorization on the returned route group.
/// </summary>
public sealed class Startup
{
    public const string Path = "/admin/settings";
    public const string Policy = "SettingsAdministrators";
    public const string AdminHeader = "X-Settings-Admin";
    public const string AuthenticationScheme = "SettingsSample";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddValiantSettings();
        services.AddAuthentication(AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, SampleAdminAuthenticationHandler>(
                AuthenticationScheme,
                _ => { });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policy, policy =>
            {
                policy.RequireRole(SampleAdminAuthenticationHandler.AdminRole);
            });
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/", () => Results.Redirect(Path));
            endpoints.MapValiantSettingsUi(options => options.Path = Path)
                .WithTags("Settings")
                .RequireAuthorization(Policy);
        });
    }
}
