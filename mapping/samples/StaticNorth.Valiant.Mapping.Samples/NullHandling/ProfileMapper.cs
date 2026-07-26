namespace StaticNorth.Valiant.Mapping.Samples.NullHandling;

[ValiantMapper]
public sealed partial class ProfileMapper : MapperDefinition<Profile, ProfileDto>
{
    public override void Configure(MappingContext<Profile, ProfileDto> context)
    {
        context.Map(source => source.DefaultValue, target => target.DefaultValue);
        context.Map(source => source.IgnoredValue, target => target.IgnoredValue)
            .IgnoreNull();
        context.Map(source => source.MappedValue, target => target.MappedValue)
            .MapNull();
        context.Map(source => source.AllowedValue, target => target.AllowedValue)
            .AllowNull();
        context.Map(source => source.FallbackValue, target => target.FallbackValue)
            .IgnoreNull()
            .Fallback("fallback");
    }
}
