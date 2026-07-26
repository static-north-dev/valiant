using StaticNorth.Valiant.Settings.DependencyInjection;
using StaticNorth.Valiant.Validation.DependencyInjection;

namespace StaticNorth.Valiant.Settings.AspNetCore.Samples.Validation;

/// <summary>
/// Demonstrates Valiant-backed Options validation and permissive missing-validator behavior.
/// </summary>
public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddValiantValidators();
        services.AddValiantSettings(ValidationConfiguration.Create(valid: true), options =>
        {
            options.ValidateWithValiant = true;
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapValidationEndpoints());
    }
}
