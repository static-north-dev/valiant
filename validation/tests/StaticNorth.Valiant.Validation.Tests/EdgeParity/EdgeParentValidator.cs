namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator]
public sealed partial class EdgeParentValidator(EdgeReusedValidator child) : ValidatorDefinition<EdgeReuseInput>
{
    public override void Configure(ValidationContext<EdgeReuseInput> context)
    {
        context.UseValidator(child);
        context.Property(x => x.Value).Satisfies((model, _) => model.Tail(), code: "tail");
    }
}
