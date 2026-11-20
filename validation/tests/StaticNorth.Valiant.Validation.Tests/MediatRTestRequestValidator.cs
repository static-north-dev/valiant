namespace StaticNorth.Valiant.Validation.Tests;

[ValiantValidator]
public sealed partial class MediatRTestRequestValidator : ValidatorDefinition<MediatRTestRequest>
{
    public override void Configure(ValidationContext<MediatRTestRequest> context)
    {
        context.Property(request => request.Name).Required(message: "Name is required.");
    }
}
