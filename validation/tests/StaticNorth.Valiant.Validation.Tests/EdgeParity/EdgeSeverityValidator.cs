namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator]
public sealed partial class EdgeSeverityValidator : ValidatorDefinition<EdgeSeverityInput>
{
    public override void Configure(ValidationContext<EdgeSeverityInput> context)
    {
        context.Property(x => x.Value).Required(severity: context.Instance.Severity(), code: "first");
        context.Property(x => x.Value).Required(code: "tail");
    }
}
