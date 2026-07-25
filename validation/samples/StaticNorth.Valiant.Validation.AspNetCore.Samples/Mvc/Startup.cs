using Valiant.Validation.AspNetCore;
using Valiant.Validation.DependencyInjection;

namespace Valiant.Validation.AspNetCore.Samples.Mvc;

/// <summary>
/// Demonstrates automatic MVC model validation. Controllers contain no manual
/// validator dependency or validation branch.
/// </summary>
public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddValiantValidators();
        services.AddControllers().AddValiantValidation();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}
