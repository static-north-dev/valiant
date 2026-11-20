namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator]
public sealed partial class EdgeBoxedValidator : ValidatorDefinition<EdgeBoxedInput>
{
    public override void Configure(ValidationContext<EdgeBoxedInput> context)
    {
        context.Property(x => x.Value).Required();
    }
}
