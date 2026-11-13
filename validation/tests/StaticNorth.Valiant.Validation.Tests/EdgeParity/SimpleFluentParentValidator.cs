namespace StaticNorth.Valiant.Validation.Tests.EdgeParity;

[ValiantValidator]
public sealed partial class SimpleFluentParentValidator(IValidator<SimpleFluentProbe> child)
    : ValidatorDefinition<SimpleFluentParent>
{
    private readonly IValidator<SimpleFluentProbe> _child = child;

    public override void Configure(ValidationContext<SimpleFluentParent> context)
    {
        context.Property(x => x.Title).Required();
        context.Property(x => x.Child).Children(nested => nested.UseValidator(_child));
    }
}
