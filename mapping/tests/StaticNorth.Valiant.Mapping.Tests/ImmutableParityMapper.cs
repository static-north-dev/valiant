namespace StaticNorth.Valiant.Mapping.Tests;

[ValiantMapper]
public sealed partial class ImmutableParityMapper : MapperDefinition<ImmutableParitySource, ImmutableParityTarget>
{
    public override void Configure(MappingContext<ImmutableParitySource, ImmutableParityTarget> context)
    {
        context.Map(source => source.NameOverride ?? "preferred", target => target.Name).When(source => source.Preferred);
        context.Map(_ => "alternate", target => target.Name).When(source => source.Alternate).Default(context.Source.FallbackName);
        context.Map(source => source.Number, target => target.Number);
        context.Map(source => source.Numbers, target => target.Numbers);
    }
}
