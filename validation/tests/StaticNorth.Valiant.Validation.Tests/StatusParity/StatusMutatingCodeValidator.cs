namespace StaticNorth.Valiant.Validation.Tests.StatusParity;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class StatusMutatingCodeValidator : ValidatorDefinition<StatusParityModel>
{
    public override void Configure(ValidationContext<StatusParityModel> context)
    {
        context.Property(x => x.Missing).Required(code: context.Instance.EnableRule(), severity: ValiantSeverity.Warning);
        context.Property(x => x.Present).When(static x => x.Enabled).Satisfies(static (x, _) => x.RunTail(false));
    }
}
