using Valiant.Sample.Models;

namespace Valiant.Sample.Scenarios;

internal static class CatalogItemSamples
{
    public static CatalogItem Valid()
    {
        return new CatalogItem
        {
            Sku = "SKU-001",
            Name = "Desk",
            Description = "compact",
            Status = ProductStatus.Active,
            StatusName = nameof(ProductStatus.Active),
            StatusValue = (int)ProductStatus.Active,
            Tags = ["featured", "public"],
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "sample",
                ["channel"] = "api"
            },
            Price = 129,
            DiscountPercent = 10,
            SortOrder = 5,
            Currency = "EUR",
            RequiresApproval = true,
            ApprovalCode = "APPROVED",
            Kind = CatalogItemKind.Physical,
            ShippingAddress = new Address
            {
                Line1 = "1 Sample Street",
                Zip = "LV-1000"
            },
            Variants =
            [
                new Variant { Code = "A", Quantity = 5 },
                new Variant { Code = "B", Quantity = 3 }
            ],
            AuditEntries =
            [
                new AuditEntry { Actor = "system" }
            ],
            NativeLoopCheckCount = 1,
            RequireDoLoopCheck = true,
            ApiToken = "token"
        };
    }

    public static CatalogItem Invalid()
    {
        return new CatalogItem
        {
            Sku = "",
            Name = "",
            Description = "too long for the warning threshold",
            Status = (ProductStatus)999,
            StatusName = "Missing",
            StatusValue = 999,
            Tags = ["x", "blocked"],
            Metadata = new Dictionary<string, string>
            {
                ["channel"] = "unknown"
            },
            Price = 0,
            DiscountPercent = 150,
            SortOrder = 0,
            Currency = "XXX",
            RequiresApproval = false,
            ApprovalCode = "should-be-empty",
            Kind = CatalogItemKind.Physical,
            ShippingAddress = new Address
            {
                Line1 = "",
                Zip = ""
            },
            Variants =
            [
                new Variant { Code = "", Quantity = 99 }
            ],
            AuditEntries =
            [
                new AuditEntry { Actor = "" }
            ],
            NativeLoopCheckCount = 1,
            RequireDoLoopCheck = true,
            ApiToken = ""
        };
    }
}
