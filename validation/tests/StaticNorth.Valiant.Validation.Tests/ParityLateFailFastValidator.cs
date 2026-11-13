namespace StaticNorth.Valiant.Validation.Tests;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class ParityLateFailFastValidator : ValidatorDefinition<FailureBehaviorModel>
{
    public override void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.Property(model => model.Name).Required(code: "NameMissing");
        context.Property(model => model.Description).Required(code: "DescriptionMissing");
        context.FailFast();
        context.Property(model => model.Name).Required(code: "AfterFailFast");
    }
}
