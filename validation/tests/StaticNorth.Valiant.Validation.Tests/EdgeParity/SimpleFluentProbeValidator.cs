namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator]
public sealed partial class SimpleFluentProbeValidator : ValidatorDefinition<SimpleFluentProbe>
{
    public override void Configure(ValidationContext<SimpleFluentProbe> context)
    {
        context.Property(x => x.Name).Required().MinLength(3);
        context.Property(x => x.Age).InclusiveBetween(18, 99);
    }
}
