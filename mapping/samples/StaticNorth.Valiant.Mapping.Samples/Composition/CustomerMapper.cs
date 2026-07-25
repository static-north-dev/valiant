namespace Valiant.Mapping.Samples.Composition;

[ValiantMapper]
public sealed partial class CustomerMapper(AddressMapper addressMapper)
    : MapperDefinition<Customer, CustomerDto>
{
    public override void Configure(MappingContext<Customer, CustomerDto> context)
    {
        context.UseMapper(addressMapper);
        context.Map(source => source.PrimaryAddress, target => target.PrimaryAddress);
        context.Map(source => source.Addresses, target => target.Addresses);
    }
}
