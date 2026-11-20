namespace StaticNorth.Valiant.Validation.Tests.StatusParity;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class StatusThrowingRuleValidator : ValidatorDefinition<StatusParityModel>
{
    public override void Configure(ValidationContext<StatusParityModel> context)
    {
        context.Property(x => x.Missing).Required();
        context.Property(x => x.Present)
            .When(static x => x.Enabled)
            .Satisfies(static (x, _) => x.ThrowTail(), code: context.Instance.EnableRule());
    }
}
