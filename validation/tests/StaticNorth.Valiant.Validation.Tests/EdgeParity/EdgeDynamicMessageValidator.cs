namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator]
public sealed partial class EdgeDynamicMessageValidator : ValidatorDefinition<EdgeDynamicMessageInput>
{
    public override void Configure(ValidationContext<EdgeDynamicMessageInput> context)
    {
        context.Property(x => x.Value).Required(message: context.Instance.Message());
    }
}
