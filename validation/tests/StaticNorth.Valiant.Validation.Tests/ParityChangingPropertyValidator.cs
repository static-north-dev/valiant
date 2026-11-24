namespace StaticNorth.Valiant.Validation.Tests;

[ValiantValidator]
public sealed partial class ParityChangingPropertyValidator : ValidatorDefinition<ParityChangingProperty>
{
    public override void Configure(ValidationContext<ParityChangingProperty> context)
    {
        context.Property(model => model.Value)
            .Required(code: "Missing", message: "Provide a value.")
            .NotEmpty(code: "Empty", message: "Value is empty.", severity: ValiantSeverity.Warning);
    }
}
