namespace Valiant.Validation.Samples.Catalog;

/// <summary>
/// Validates sample addresses.
/// </summary>
[ValiantValidator]
public sealed partial class AddressValidator : ValidatorDefinition<Address>
{
    public override void Configure(ValidationContext<Address> context)
    {
        context.Property(address => address.Line1)
            .Required();
                    
        context.Property(address => address.Line2)
            .Null(severity: ValiantSeverity.Info);

        context.Property(address => address.Zip)
            .Required()
            .LengthBetween(4, 12);
    }
}
