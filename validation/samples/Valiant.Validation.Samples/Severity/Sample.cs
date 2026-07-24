using Microsoft.Extensions.DependencyInjection;
using Valiant.Validation.DependencyInjection;
using Valiant.Validation.Samples.Catalog;

namespace Valiant.Validation.Samples.Severity;

/// <summary>
/// Demonstrates error, warning, and information severities and shows that
/// warning-only and information-only failures do not invalidate a result.
/// </summary>
public static class Sample
{
    public static async Task RunAsync()
    {
        await using var provider = CreateProvider();
        var validator = provider.GetRequiredService<CatalogItemValidator>();
        var warningAndInfo = CatalogItemFactory.Valid() with
        {
            Description = "This description is intentionally longer than the warning threshold.",
            ShippingAddress = CatalogItemFactory.Valid().ShippingAddress! with
            {
                Line2 = "Optional second line"
            }
        };

        var result = await validator.ValidateAsync(warningAndInfo);

        Console.WriteLine($"Warning-and-information result is valid: {result.IsValid}");
        Console.WriteLine($"Errors: {result.Errors.Count}");
        Console.WriteLine($"Warnings: {result.Warnings.Count}");
        Console.WriteLine($"Information: {result.Infos.Count}");
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddValiantValidators(options => options.Mode = ValiantValidationMode.Fast);
        return services.BuildServiceProvider();
    }
}
