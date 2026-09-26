namespace StaticNorth.Valiant.Validation.Tests;

[ValiantValidator]
public sealed partial class DynamicFailureMessageValidator : ValidatorDefinition<NullableTextModel>
{
    public string Message { get; set; } = "first";

    public override void Configure(ValidationContext<NullableTextModel> context)
    {
        context.Property(model => model.Value).Required(message: Message);
    }
}
