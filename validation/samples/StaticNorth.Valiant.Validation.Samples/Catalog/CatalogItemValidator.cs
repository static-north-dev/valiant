namespace Valiant.Validation.Samples.Catalog;

/// <summary>
/// Validates sample catalog items.
/// </summary>
[ValiantValidator]
public sealed partial class CatalogItemValidator(
    CatalogItemRulesValidator rulesValidator) : ValidatorDefinition<CatalogItem>
{
    public override void Configure(ValidationContext<CatalogItem> context)
    {
        context.UseValidator(rulesValidator);

        context.Property(item => item.Sku)
            .SatisfiesAsync(
                static (sku, _) => new ValueTask<bool>(sku is not "RESERVED"),
                code: "ReservedSku",
                message: "SKU is reserved.");
    }
}
