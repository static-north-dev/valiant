using StaticNorth.Valiant.Settings.DependencyInjection;
using StaticNorth.Valiant.Validation.DependencyInjection;

namespace StaticNorth.Valiant.Settings.AspNetCore.Samples.Validation;

/// <summary>
/// Demonstrates translated Options validation failures for invalid configured values.
/// </summary>
public sealed class InvalidStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddValiantValidators();
        services.AddValiantSettings(ValidationConfiguration.Create(valid: false), options =>
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
