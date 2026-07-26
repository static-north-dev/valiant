using StaticNorth.Valiant.Settings.DependencyInjection;
using StaticNorth.Valiant.Validation.DependencyInjection;

namespace StaticNorth.Valiant.Settings.AspNetCore.Samples.Validation;

/// <summary>
/// Demonstrates required validator enforcement for configured settings models.
/// </summary>
public sealed class RequiredStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddValiantValidators();
        services.AddValiantSettings(ValidationConfiguration.Create(valid: true), options =>
        {
            options.ValidateWithValiant = true;
            options.RequireValidators = true;
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapValidationEndpoints());
    }
}
