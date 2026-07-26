using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.Samples.Basic;

/// <summary>
/// Demonstrates default endpoint discovery and mapping for standalone and grouped endpoints.
/// </summary>
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddValiantEndpoints();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapValiantEndpoints());
    }
}
