namespace StaticNorth.Valiant.Validation.Tests;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class ParityConditionalAsyncValidator : ValidatorDefinition<ParityChangingProperty>
{
    public override void Configure(ValidationContext<ParityChangingProperty> context)
    {
        if (context.Instance.EnableAsyncRule)
        {
            context.Property(model => model.Value)
                .SatisfiesAsync(static (value, _) => new ValueTask<bool>(value is not null), code: "AsyncMissing");
        }
    }
}
