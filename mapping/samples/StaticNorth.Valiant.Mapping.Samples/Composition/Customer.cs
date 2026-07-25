namespace Valiant.Mapping.Samples.Composition;

public sealed record Customer
{
    public Address PrimaryAddress { get; init; } = new("");

    public List<Address> Addresses { get; init; } = [];
}
