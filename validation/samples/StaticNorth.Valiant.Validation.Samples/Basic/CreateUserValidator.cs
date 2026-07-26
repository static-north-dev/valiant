namespace StaticNorth.Valiant.Validation.Samples.Basic;

[ValiantValidator]
public sealed partial class CreateUserValidator : ValidatorDefinition<CreateUser>
{
    public override void Configure(ValidationContext<CreateUser> context)
    {
        context.Property(user => user.Name)
            .Required(message: "Name is required.")
            .MinLength(3);
    }
}
