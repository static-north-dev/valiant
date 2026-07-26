using Microsoft.Extensions.DependencyInjection;
using StaticNorth.Valiant.Validation.DependencyInjection;
using StaticNorth.Valiant.Validation.Samples.Catalog;

namespace StaticNorth.Valiant.Validation.Samples.ControlFlowAndCollections;

/// <summary>
/// Demonstrates collection and dictionary rules, conditional and switch-based
/// rules, indexed item validation, and native foreach, for, while, and do loops.
/// </summary>
public static class Sample
{
    public static Task RunAsync()
    {
        using var provider = CreateProvider();
        var validator = provider.GetRequiredService<CatalogItemRulesValidator>();
        var result = validator.Validate(CatalogItemFactory.Invalid());

        foreach (var failure in result.Failures.Where(static failure =>
                     failure.PropertyName is "Tags" or "Metadata" or "ApprovalCode" or "ApiToken" ||
                     failure.ErrorCode is "ForLoopSkuRequired" or "WhileLoopNameRequired" or "DoLoopApiTokenRequired" ||
                     failure.PropertyName.StartsWith("Variants", StringComparison.Ordinal) ||
                     failure.PropertyName.StartsWith("AuditEntries", StringComparison.Ordinal)))
        {
            Console.WriteLine($"{failure.PropertyName}: {failure.ErrorCode}");
        }

        return Task.CompletedTask;
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddValiantValidators(options => options.Mode = ValiantValidationMode.Fast);
        return services.BuildServiceProvider();
    }
}
