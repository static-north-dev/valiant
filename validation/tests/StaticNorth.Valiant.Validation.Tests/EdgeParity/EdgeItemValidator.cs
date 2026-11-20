namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class EdgeItemValidator : ValidatorDefinition<EdgeChildInput>
{
    public override void Configure(ValidationContext<EdgeChildInput> context)
    {
        context.Property(x => x.Items).ForEach(item =>
        {
            item.ValidateAll();
            item.Property(x => x.Counted).Required(code: "item1");
            item.Property(x => x.Counted).Required(code: "item2");
        });
        context.Property(x => x.Child).Satisfies(static _ => false, code: "tail");
    }
}
