namespace StaticNorth.Valiant.Validation.Tests.StatusParity;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class StatusLateMessageValidator : ValidatorDefinition<StatusParityModel>
{
    public override void Configure(ValidationContext<StatusParityModel> context)
    {
        context.Property(x => x.Present).When(static x => x.Enabled).Satisfies(static (x, _) => x.RunTail(false));
        context.Property(x => x.Present).Required(message: context.Instance.EnableRule());
    }
}
