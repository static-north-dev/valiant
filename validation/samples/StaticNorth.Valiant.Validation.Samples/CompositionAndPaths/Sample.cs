using Microsoft.Extensions.DependencyInjection;
using StaticNorth.Valiant.Validation.DependencyInjection;
using StaticNorth.Valiant.Validation.Samples.Catalog;

namespace StaticNorth.Valiant.Validation.Samples.CompositionAndPaths;

/// <summary>
/// Demonstrates whole-model validator composition, reusable property validators,
/// and nested or indexed failure paths.
/// </summary>
public static class Sample
{
    public static async Task RunAsync()
    {
        await using var provider = CreateProvider();
        var validator = provider.GetRequiredService<CatalogItemValidator>();
        var result = await validator.ValidateAsync(CatalogItemFactory.Invalid());

        foreach (var failure in result.Failures.Where(static failure =>
                     failure.PropertyName.StartsWith("ShippingAddress", StringComparison.Ordinal) ||
                     failure.PropertyName.StartsWith("Variants", StringComparison.Ordinal) ||
                     failure.PropertyName.StartsWith("AuditEntries", StringComparison.Ordinal)))
        {
            Console.WriteLine($"{failure.PropertyName}: {failure.ErrorCode}");
        }
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddValiantValidators(options => options.Mode = ValiantValidationMode.Fast);
        return services.BuildServiceProvider();
    }
}
