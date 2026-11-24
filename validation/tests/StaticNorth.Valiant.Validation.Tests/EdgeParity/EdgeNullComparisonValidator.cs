namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator]
public sealed partial class EdgeNullComparisonValidator : ValidatorDefinition<EdgeNullComparisonInput>
{
    public override void Configure(ValidationContext<EdgeNullComparisonInput> context)
    {
        context.Property(x => x.Value).GreaterThan("a");
    }
}
