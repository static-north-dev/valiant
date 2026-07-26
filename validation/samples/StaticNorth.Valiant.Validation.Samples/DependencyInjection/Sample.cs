using Microsoft.Extensions.DependencyInjection;
using StaticNorth.Valiant.Validation.DependencyInjection;
using StaticNorth.Valiant.Validation.Samples.Catalog;

namespace StaticNorth.Valiant.Validation.Samples.DependencyInjection;

/// <summary>
/// Demonstrates generated validator registration, configurable lifetime and
/// defaults, concrete non-default validators, and generated metadata providers.
/// </summary>
public static class Sample
{
    public static Task RunAsync()
    {
        var services = new ServiceCollection();
        services.AddValiantValidators(options =>
        {
            options.Mode = ValiantValidationMode.Fast;
            options.Lifetime = ValiantValidatorLifetime.Scoped;
            options.FailureBehavior = ValiantFailureBehavior.ValidateAll;
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var validator = scope.ServiceProvider.GetRequiredService<IValidator<CatalogItem>>();
        var reusableRules = scope.ServiceProvider.GetRequiredService<CatalogItemRulesValidator>();
        var propertyValidator = scope.ServiceProvider.GetRequiredService<AddressPropertyValidator>();
        var metadata = scope.ServiceProvider.GetServices<IValiantValidationMetadataProvider>()
            .Single(item => item.ModelType == typeof(CatalogItem));

        Console.WriteLine($"Default validator: {validator.GetType().Name}");
        Console.WriteLine($"Non-default concrete validator: {reusableRules.GetType().Name}");
        Console.WriteLine($"Property validator: {propertyValidator.GetType().Name}");
        Console.WriteLine($"Metadata model: {metadata.ModelType.Name}");
        return Task.CompletedTask;
    }
}
