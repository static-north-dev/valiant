namespace StaticNorth.Valiant.Mapping.Samples.Composition;

public sealed record CustomerDto
{
    public AddressDto PrimaryAddress { get; set; } = new("");

    public List<AddressDto> Addresses { get; set; } = [];
}
