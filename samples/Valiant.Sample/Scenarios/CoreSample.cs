using Microsoft.Extensions.DependencyInjection;
using Valiant.DependencyInjection;
using Valiant.Sample.Models;
using Valiant.Sample.Validation;

namespace Valiant.Sample.Scenarios;

internal static class CoreSample
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();
        services.AddValiantValidations(options =>
        {
            options.Mode = ValiantValidationMode.Fast;
            options.Lifetime = ValiantValidatorLifetime.Transient;
            options.FailureBehavior = ValiantFailureBehavior.ValidateAll;
        });

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<CatalogItemValidator>();
        var staticValidator = provider.GetRequiredService<CatalogItemRulesValidator>();
        var metadataProviders = provider.GetServices<IValiantValidationMetadataProvider>().ToArray();

        SampleAssertions.Require(
            metadataProviders.Any(provider => provider.ModelType == typeof(CatalogItem)),
            "Generated validation metadata was not registered.");
        Console.WriteLine("DI and generated metadata registration passed.");

        var valid = CatalogItemSamples.Valid();
        Console.WriteLine("Running valid async validation.");
        var validResult = await validator.ValidateAsync(valid);
        SampleAssertions.Require(validResult.IsValid, "Generated validator rejected a valid catalog item.");
        SampleAssertions.Require(await validator.IsValidAsync(valid), "Generated async boolean validation rejected a valid catalog item.");

        Console.WriteLine("Running valid sync validation.");
        SampleAssertions.Require(staticValidator.IsValid(valid), "Synchronous validator rejected a valid catalog item.");

        try
        {
            Console.WriteLine("Checking sync guard for async validators.");
            _ = validator.Validate(valid);
            throw new InvalidOperationException("Synchronous validation should fail when a validator contains async checks.");
        }
        catch (InvalidOperationException)
        {
        }

        var warningOnly = CatalogItemSamples.Valid() with
        {
            Description = "This description is intentionally longer than the warning threshold."
        };
        Console.WriteLine("Running warning-only validation.");
        var warningResult = await validator.ValidateAsync(warningOnly);
        SampleAssertions.Require(warningResult.IsValid, "Warning-only validation should not make the result invalid.");
        SampleAssertions.Require(warningResult.Warnings.Count == 1, "Expected one warning for the long description.");
        SampleAssertions.Require(warningResult.Failures.Count == 1, "Warnings should still be present in the full failure list.");
        warningResult.ThrowIfFailed();

        var invalid = CatalogItemSamples.Invalid();
        Console.WriteLine("Running invalid async validation.");
        var invalidResult = await validator.ValidateAsync(invalid);
        SampleAssertions.Require(!invalidResult.IsValid, "Generated validator accepted an invalid catalog item.");
        SampleAssertions.RequireFailure(invalidResult, "Sku", "SkuRequired");
        SampleAssertions.RequireFailure(invalidResult, "Name", "NameRequired");
        SampleAssertions.RequireNoFailure(invalidResult, "Name", "NameTooShort");
        SampleAssertions.RequireFailure(invalidResult, "Status", "DefinedEnum");
        SampleAssertions.RequireFailure(invalidResult, "StatusName", "EnumName");
        SampleAssertions.RequireFailure(invalidResult, "StatusValue", "EnumValue");
        SampleAssertions.RequireFailure(invalidResult, "Tags", "Contains");
        SampleAssertions.RequireFailure(invalidResult, "Metadata", "HasKey");
        SampleAssertions.RequireFailure(invalidResult, "Price", "GreaterThan");
        SampleAssertions.RequireFailure(invalidResult, "DiscountPercent", "InclusiveBetween");
        SampleAssertions.RequireFailure(invalidResult, "ShippingAddress.Line1", "Required");
        SampleAssertions.RequireFailure(invalidResult, "Variants[0].Code", "Required");
        SampleAssertions.RequireFailure(invalidResult, "AuditEntries[0].Actor", "Required");
        SampleAssertions.RequireFailure(invalidResult, "ApiToken", "Required");

        try
        {
            invalidResult.ThrowIfFailed();
            throw new InvalidOperationException("ThrowIfFailed should throw for validation errors.");
        }
        catch (ValiantValidationException exception)
        {
            SampleAssertions.Require(
                ReferenceEquals(exception.Result, invalidResult),
                "Validation exception should expose the original result.");
        }

        Console.WriteLine("Valiant core sample passed.");
    }
}
