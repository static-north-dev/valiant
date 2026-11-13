namespace StaticNorth.Valiant.Validation.Tests.StatusParity;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class StatusThrowingMessageValidator : ValidatorDefinition<StatusParityModel>
{
    public override void Configure(ValidationContext<StatusParityModel> context)
    {
        context.Property(x => x.Present).Required(message: context.Instance.ThrowMessage());
        context.Property(x => x.Present).Satisfies(static (x, _) => x.RunTail(true));
    }
}
