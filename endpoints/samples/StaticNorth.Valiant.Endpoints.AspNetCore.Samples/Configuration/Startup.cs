using Valiant.Endpoints.AspNetCore.DependencyInjection;

namespace Valiant.Endpoints.AspNetCore.Samples.Configuration;

/// <summary>
/// Demonstrates endpoint-group and endpoint configuration hooks during mapping.
/// </summary>
public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddValiantEndpoints(options =>
        {
            options.ConfigureEndpointGroup = (group, registration) =>
            {
                if (registration.GroupType != typeof(ConfigurationEndpointGroup))
                {
                    return;
                }

                group.WithTags("Configuration");
                group.AddEndpointFilter(async (context, next) =>
                {
                    context.HttpContext.Response.Headers["X-Valiant-Group"] =
                        registration.GroupType.Name;
                    return await next(context);
                });
            };

            options.ConfigureEndpoint = (endpoint, registration) =>
            {
                if (registration.Group?.GroupType != typeof(ConfigurationEndpointGroup) ||
                    registration.EndpointType != typeof(SelectedEndpoint))
                {
                    return;
                }

                endpoint.WithName("ConfigurationSelected");
                endpoint.AddEndpointFilter(async (context, next) =>
                {
                    context.HttpContext.Response.Headers["X-Valiant-Endpoint"] =
                        registration.EndpointType.Name;
                    return await next(context);
                });
            };
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapValiantEndpoints());
    }
}
