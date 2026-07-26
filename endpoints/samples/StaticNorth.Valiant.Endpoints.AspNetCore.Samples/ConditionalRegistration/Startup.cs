using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.Samples.ConditionalRegistration;

/// <summary>
/// Demonstrates conditional registration of endpoint groups and individual endpoints.
/// </summary>
public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddValiantEndpoints(options =>
        {
            options.MapEndpointGroupsWhen = registration =>
                registration.GroupType != typeof(BlockedEndpointGroup);

            options.MapEndpointWhen = registration =>
                registration.EndpointType != typeof(BlockedStandaloneEndpoint);
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapValiantEndpoints());
    }
}
