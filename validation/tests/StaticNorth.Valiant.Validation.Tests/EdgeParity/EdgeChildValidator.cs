namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator]
public sealed partial class EdgeChildValidator : ValidatorDefinition<EdgeChildInput>
{
    public override void Configure(ValidationContext<EdgeChildInput> context)
    {
        context.Property(x => x.Child).Children(child =>
        {
            child.ValidateAll();
            child.Property(x => x.Counted).Required(code: "child1");
            child.Property(x => x.Counted).Required(code: "child2");
        });
        context.Property(x => x.Child).Satisfies(static _ => false, code: "tail");
    }
}
