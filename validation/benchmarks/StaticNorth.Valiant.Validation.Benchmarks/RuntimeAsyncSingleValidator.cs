namespace StaticNorth.Valiant.Validation.Benchmarks;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class RuntimeAsyncSingleValidator : ValidatorDefinition<RuntimeAsyncValidationInput>
{
    public override void Configure(ValidationContext<RuntimeAsyncValidationInput> context)
    {
        context.Property(static model => model.Rule)
            .SatisfiesAsync(static (rule, _) => rule.Evaluate());
    }
}
