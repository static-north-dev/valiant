using Valiant.Endpoints.AspNetCore.DependencyInjection;

namespace Valiant.Endpoints.AspNetCore.Samples.QueryBinding;

/// <summary>
/// Demonstrates generated query binding for arrays, objects, and supported query formats.
/// </summary>
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddValiantEndpoints(options =>
        {
            options.QueryParameters.EnableBinding = true;
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapValiantEndpoints());
    }
}
