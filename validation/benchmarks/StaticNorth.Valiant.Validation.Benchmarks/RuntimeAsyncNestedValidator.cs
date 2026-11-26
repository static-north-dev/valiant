namespace StaticNorth.Valiant.Validation.Benchmarks;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class RuntimeAsyncNestedValidator(
    RuntimeAsyncSingleValidator inner) : ValidatorDefinition<RuntimeAsyncValidationInput>
{
    public override void Configure(ValidationContext<RuntimeAsyncValidationInput> context)
    {
        context.UseValidator(inner);
    }
}
