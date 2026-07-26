using Microsoft.Extensions.DependencyInjection;
using StaticNorth.Valiant.Validation.DependencyInjection;
using StaticNorth.Valiant.Validation.Samples.Catalog;

namespace StaticNorth.Valiant.Validation.Samples.FailureHandling;

/// <summary>
/// Demonstrates per-property fail-fast behavior, result helpers, and converting
/// an invalid result into a <see cref="ValiantValidationException"/>.
/// </summary>
public static class Sample
{
    public static async Task RunAsync()
    {
        await using var provider = CreateProvider();
        var validator = provider.GetRequiredService<CatalogItemValidator>();
        var result = await validator.ValidateAsync(CatalogItemFactory.Invalid());

        var nameFailures = result.Failures.Where(static failure =>
            failure.PropertyName == "Name" &&
            failure.ErrorCode is "NameRequired" or "NameTooShort" or "MaxLength").ToArray();
        Console.WriteLine($"Name failures after FailFast: {nameFailures.Length}");

        try
        {
            result.ThrowIfFailed();
        }
        catch (ValiantValidationException exception)
        {
            Console.WriteLine($"ThrowIfFailed produced {exception.GetType().Name} with {exception.Errors.Count} errors.");
        }
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddValiantValidators(options => options.Mode = ValiantValidationMode.Fast);
        return services.BuildServiceProvider();
    }
}
