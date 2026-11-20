namespace StaticNorth.Valiant.Validation.Tests.StatusParity;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class StatusReusedValidator(IValidator<StatusParityModel> dependency) : ValidatorDefinition<StatusParityModel>
{
    private readonly IValidator<StatusParityModel> _dependency = dependency;

    public override void Configure(ValidationContext<StatusParityModel> context)
    {
        context.UseValidator(_dependency);
        context.Property(x => x.Present).When(static x => x.ReuseMethod != null).Satisfies(static (x, _) => x.RunTail(false));
    }
}
