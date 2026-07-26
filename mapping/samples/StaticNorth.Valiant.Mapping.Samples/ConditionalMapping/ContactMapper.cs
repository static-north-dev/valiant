namespace StaticNorth.Valiant.Mapping.Samples.ConditionalMapping;

[ValiantMapper]
public sealed partial class ContactMapper : MapperDefinition<Contact, ContactDto>
{
    public override void Configure(MappingContext<Contact, ContactDto> context)
    {
        if (context.Source.UseEmailAsDisplayName)
        {
            context.Map(source => source.Email, target => target.DisplayName);
        }
        else
        {
            context.Map(source => source.Name, target => target.DisplayName);
        }

        context
            .Map(source => source.Email, target => target.Email)
            .Fallback("Private email")
            .Map(source => source.Phone, target => target.Phone)
            .Default("Private phone")
            .When(source => source.IncludeContactDetails);
    }
}
