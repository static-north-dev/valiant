namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class EdgeChildPredicateValidator : ValidatorDefinition<EdgeChildInput>
{
    public override void Configure(ValidationContext<EdgeChildInput> context)
    {
        context.Property(x => x.Child).Children(child =>
            child.Property(x => x.Counted).Satisfies(static (model, value) => model.Value == value && value == "match", code: "child"));
    }
}
