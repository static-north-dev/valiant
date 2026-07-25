using Microsoft.Extensions.Configuration;

namespace Valiant.Settings.AspNetCore.Samples.Validation;

internal static class ValidationConfiguration
{
    public static IConfiguration Create(bool valid)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Samples:AspNetCore:Validation:Validated:Name"] = valid ? "catalog" : "",
                ["Samples:AspNetCore:Validation:Validated:RetryCount"] = valid ? "3" : "0",
                ["Samples:AspNetCore:Validation:Missing:Enabled"] = "true"
            })
            .Build();
    }
}
