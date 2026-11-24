namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class EdgeChildAsyncPredicateValidator : ValidatorDefinition<EdgeChildInput>
{
    public override void Configure(ValidationContext<EdgeChildInput> context)
    {
        context.Property(x => x.Child).Children(child =>
            child.Property(x => x.Counted).SatisfiesAsync(static (model, value, _) => ValueTask.FromResult(model.Value == value && value == "match"), code: "async-child"));
    }
}
