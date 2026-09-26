namespace StaticNorth.Valiant.Validation.Tests;

[ValiantValidator]
public sealed partial class FixedFailureValidator : ValidatorDefinition<FixedFailureModel>
{
    public override void Configure(ValidationContext<FixedFailureModel> context)
    {
        context.Property(model => model.A).GreaterThan(0);
        context.Property(model => model.B).GreaterThan(0);
        context.Property(model => model.C).GreaterThan(0);
        context.Property(model => model.D).GreaterThan(0);
        context.Property(model => model.E).GreaterThan(0);
    }
}
