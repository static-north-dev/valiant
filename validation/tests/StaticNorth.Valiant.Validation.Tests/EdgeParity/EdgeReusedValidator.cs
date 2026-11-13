namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class EdgeReusedValidator : ValidatorDefinition<EdgeReuseInput>
{
    public override void Configure(ValidationContext<EdgeReuseInput> context)
    {
        context.Property(x => x.Value).Required(code: "inner1");
        context.Property(x => x.Value).Required(code: "inner2");
    }
}
