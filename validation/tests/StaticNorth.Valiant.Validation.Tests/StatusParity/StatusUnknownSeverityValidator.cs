namespace StaticNorth.Valiant.Validation.Tests.StatusParity;

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class StatusUnknownSeverityValidator(UnknownSeverityScope scope, IPropertyValidator<string> propertyValidator) : ValidatorDefinition<StatusParityModel>
{
    private readonly UnknownSeverityScope _scope = scope;
    private readonly IPropertyValidator<string> _propertyValidator = propertyValidator;

    public override void Configure(ValidationContext<StatusParityModel> context)
    {
        switch (_scope)
        {
            case UnknownSeverityScope.Property:
                context.Property(x => x.Missing).Required(severity: (ValiantSeverity)99);
                break;
            case UnknownSeverityScope.Child:
                context.Property(x => x.Child).Children(child =>
                    child.Property(x => x.Missing).Required(severity: (ValiantSeverity)99));
                break;
            case UnknownSeverityScope.ReusedProperty:
                context.Property(x => x.Present).UseValidator(_propertyValidator);
                break;
        }

        context.Property(x => x.Present).Satisfies(static (x, _) => x.RunTail(true));
    }
}
