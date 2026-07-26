using Microsoft.Extensions.DependencyInjection;
using StaticNorth.Valiant.Validation.DependencyInjection;
using StaticNorth.Valiant.Validation.Samples.Catalog;

namespace StaticNorth.Valiant.Validation.Samples.SyncAndAsyncValidation;

/// <summary>
/// Demonstrates synchronous validation, asynchronous rules, boolean helpers,
/// and the guard against synchronously invoking an asynchronous validator.
/// </summary>
public static class Sample
{
    public static async Task RunAsync()
    {
        await using var provider = CreateProvider();
        var asyncValidator = provider.GetRequiredService<CatalogItemValidator>();
        var syncValidator = provider.GetRequiredService<CatalogItemRulesValidator>();
        var valid = CatalogItemFactory.Valid();

        Console.WriteLine($"Synchronous result: {syncValidator.IsValid(valid)}");
        Console.WriteLine($"Asynchronous result: {await asyncValidator.IsValidAsync(valid)}");

        try
        {
            _ = asyncValidator.Validate(valid);
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("asynchronous", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Expected sync guard: {exception.Message}");
        }
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddValiantValidators(options => options.Mode = ValiantValidationMode.Fast);
        return services.BuildServiceProvider();
    }
}
