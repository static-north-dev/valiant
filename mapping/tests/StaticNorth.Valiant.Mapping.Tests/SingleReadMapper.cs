namespace StaticNorth.Valiant.Mapping.Tests;

[ValiantMapper]
public sealed partial class SingleReadMapper : MapperDefinition<SingleReadSource, SingleReadTarget>
{
    public override void Configure(MappingContext<SingleReadSource, SingleReadTarget> context)
    {
        context.Map(source => source.ConstructorValue, target => target.ConstructorValue).Default("fallback");
        context.Map(source => source.InitValue, target => target.InitValue);
        context.Map(source => source.Value, target => target.Value)
            .When(source => !source.SkipValue)
            .IgnoreNull()
            .Default("original");
    }
}
