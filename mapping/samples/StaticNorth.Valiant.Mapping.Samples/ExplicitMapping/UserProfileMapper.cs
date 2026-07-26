namespace StaticNorth.Valiant.Mapping.Samples.ExplicitMapping;

[ValiantMapper]
public sealed partial class UserProfileMapper : MapperDefinition<UserProfile, UserProfileDto>
{
    public override void Configure(MappingContext<UserProfile, UserProfileDto> context)
    {
        context.Map(
            source => $"{source.FirstName} {source.LastName}",
            target => target.DisplayName);
        context.Map(source => source.EmailAddress, target => target.Email);
        context.Ignore(target => target.InternalNote);
    }
}
