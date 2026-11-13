namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator]
public sealed partial class EdgeThrowingValidator : ValidatorDefinition<EdgeThrowingInput>
{
    public override void Configure(ValidationContext<EdgeThrowingInput> context)
    {
        context.Property(x => x.Child!.Value).Required();
    }
}
