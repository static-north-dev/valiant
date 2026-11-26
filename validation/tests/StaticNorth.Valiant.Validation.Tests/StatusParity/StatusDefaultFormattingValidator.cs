namespace StaticNorth.Valiant.Validation.Tests.StatusParity;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class StatusDefaultFormattingValidator : ValidatorDefinition<StatusParityModel>
{
    public override void Configure(ValidationContext<StatusParityModel> context)
    {
        context.Property(x => x.Rank).GreaterThan(context.Instance.MinimumRank);
        context.Property(x => x.Present).Satisfies(static (x, _) => x.RunTail(true));
    }
}
