namespace Valiant.Validation.Sample.Models;

public sealed record CatalogItem
{
    public string? Sku { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public ProductStatus Status { get; init; }

    public string? StatusName { get; init; }

    public int StatusValue { get; init; }

    public List<string> Tags { get; init; } = [];

    public Dictionary<string, string> Metadata { get; init; } = [];

    public decimal Price { get; init; }

    public int DiscountPercent { get; init; }

    public int SortOrder { get; init; }

    public string? Currency { get; init; }

    public bool RequiresApproval { get; init; }

    public string? ApprovalCode { get; init; }

    public CatalogItemKind Kind { get; init; }

    public Address? ShippingAddress { get; init; }

    public List<Variant> Variants { get; init; } = [];

    public List<AuditEntry> AuditEntries { get; init; } = [];

    public int NativeLoopCheckCount { get; init; }

    public bool RequireDoLoopCheck { get; init; }

    public string? ApiToken { get; init; }
}
