using Valiant.Validation.Sample.Models;

namespace Valiant.Validation.Sample.Validation;

[ValiantValidator]
public sealed partial class AddressValidator : IValidatorDefinition<Address>
{
    public static void Configure(ValidationContext<Address> context)
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
