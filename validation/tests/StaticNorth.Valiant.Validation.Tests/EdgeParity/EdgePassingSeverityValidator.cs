namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class EdgePassingSeverityValidator : ValidatorDefinition<EdgeSeverityInput>
{
    public override void Configure(ValidationContext<EdgeSeverityInput> context)
    {
        context.Property(x => x.Value)
            .Empty(severity: context.Instance.Severity())
            .Required(severity: context.Instance.Severity(), code: "warning");
    }
}
