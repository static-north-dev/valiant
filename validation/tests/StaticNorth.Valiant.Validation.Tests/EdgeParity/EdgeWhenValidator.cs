namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator]
public sealed partial class EdgeWhenValidator : ValidatorDefinition<EdgeWhenInput>
{
    public override void Configure(ValidationContext<EdgeWhenInput> context)
    {
        context.Property(x => x.Value).When(x => x.TestCondition())
            .Required(code: "required").NotEmpty(code: "notEmpty");
    }
}
