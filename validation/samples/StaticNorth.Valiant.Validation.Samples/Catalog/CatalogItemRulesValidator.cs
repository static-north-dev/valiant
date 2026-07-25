namespace Valiant.Validation.Samples.Catalog;

/// <summary>
/// Defines reusable validation rules for sample catalog items.
/// </summary>
[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class CatalogItemRulesValidator(AddressPropertyValidator addressValidator) : ValidatorDefinition<CatalogItem>
{
    public override void Configure(ValidationContext<CatalogItem> context)
    {
        context.ValidateAll();

        context.Property(item => item.Sku)
            .Required(code: "SkuRequired", message: "SKU is required.")
            .LengthBetween(3, 16);

        context.Property(item => item.Name)
            .FailFast()
            .Required(code: "NameRequired", message: "Name is required.")
            .MinLength(3, code: "NameTooShort")
            .MaxLength(40);

        context.Property(item => item.Description)
            .MaxLength(10, severity: ValiantSeverity.Warning);

        context.Property(item => item.Status)
            .DefinedEnum();

        context.Property(item => item.StatusName)
            .EnumName<ProductStatus>();

        context.Property(item => item.StatusValue)
            .EnumValue<ProductStatus>();

        context.Property(item => item.Tags)
            .ValidateAll()
            .NotEmpty()
            .LengthBetween(1, 4)
            .MinLength(1)
            .MaxLength(4)
            .Contains("public")
            .Excludes("blocked")
            .All(static tag => tag.Length > 1)
            .Any(static tag => tag == "featured")
            .None(static tag => tag == "deprecated");

        context.Property(item => item.Metadata)
            .HasKey("source")
            .HasValue("sample");

        context.Property(item => item.Price)
            .GreaterThan(0)
            .GreaterThanOrEqualTo(1)
            .LessThan(10000)
            .LessThanOrEqualTo(9999);

        context.Property(item => item.DiscountPercent)
            .InclusiveBetween(0, 90);

        context.Property(item => item.SortOrder)
            .ExclusiveBetween(0, 100);

        context.Property(item => item.Currency)
            .Equal("EUR")
            .NotEqual("XXX");

        context.Property(item => item.Name)
            .Satisfies(static (_, name) => name is null || !name.Contains('!'), code: "NameContainsBang");

        if (context.Instance.RequiresApproval)
        {
            context.Property(item => item.ApprovalCode)
                .Required();
        }
        else
        {
            context.Property(item => item.ApprovalCode)
                .Empty();
        }

        switch (context.Instance.Kind)
        {
            case CatalogItemKind.Physical:
                context.Property(item => item.ShippingAddress)
                    .Required()
                    .UseValidator(addressValidator);
                break;
            case CatalogItemKind.Digital:
                context.Property(item => item.ShippingAddress)
                    .Null();
                break;
        }

        context.Property(item => item.Variants).ForEach(variant =>
        {
            variant.Property(item => item.Code)
                .Required();
        });

        context.Property(item => item.Variants).ForEach((variant, variants) =>
        {
            variant.Property(item => item.Quantity)
                .InclusiveBetween(1, variants.Count * 10);
        });

        var index = 0;
        foreach (var entry in context.Instance.AuditEntries)
        {
            context.Item(entry, item => item.AuditEntries, index)
                .Property(item => item.Actor)
                .Required();
            index++;
        }

        for (var loopIndex = 0; loopIndex < context.Instance.NativeLoopCheckCount; loopIndex++)
        {
            context.Property(item => item.Sku)
                .Required(code: "ForLoopSkuRequired");
        }

        var remaining = context.Instance.NativeLoopCheckCount;
        while (remaining > 0)
        {
            context.Property(item => item.Name)
                .Required(code: "WhileLoopNameRequired");
            remaining--;
        }

        do
        {
            if (context.Instance.RequireDoLoopCheck)
            {
                context.Property(item => item.ApiToken)
                    .Required(code: "DoLoopApiTokenRequired");
            }
        }
        while (false);
    }
}
