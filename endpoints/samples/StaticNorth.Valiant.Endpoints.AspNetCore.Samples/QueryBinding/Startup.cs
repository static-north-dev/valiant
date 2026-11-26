using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;

[assembly: StaticNorth.Valiant.Endpoints.AspNetCore.ValiantGenerateQueryBinding]

namespace StaticNorth.Valiant.Endpoints.AspNetCore.Samples.QueryBinding;

/// <summary>
/// Demonstrates generated query binding for arrays, objects, and supported query formats.
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
