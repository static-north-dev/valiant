using Valiant.Sample.Models;

namespace Valiant.Sample.Validation;

[ValiantValidator]
public sealed partial class CatalogItemValidator(CatalogItemRulesValidator rulesValidator) : IValidatorDefinition<CatalogItem>
{
    public void Configure(ValidationContext<CatalogItem> context)
    {
        context.UseValidator(rulesValidator);

        context.Property(item => item.Sku)
            .SatisfiesAsync(
                static (sku, _) => new ValueTask<bool>(sku is null || sku != "RESERVED"),
                code: "ReservedSku",
                message: "SKU is reserved.");
    }
}
