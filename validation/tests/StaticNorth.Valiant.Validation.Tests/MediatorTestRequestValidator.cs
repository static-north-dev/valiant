namespace StaticNorth.Valiant.Validation.Tests;

[ValiantValidator]
public sealed partial class MediatorTestRequestValidator : ValidatorDefinition<MediatorTestRequest>
{
    public override void Configure(ValidationContext<MediatorTestRequest> context)
    {
        context.Property(request => request.Name).Required(message: "Name is required.");
    }
}
