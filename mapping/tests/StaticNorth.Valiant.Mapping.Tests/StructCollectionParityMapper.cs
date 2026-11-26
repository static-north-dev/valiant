namespace StaticNorth.Valiant.Mapping.Tests;

[ValiantMapper]
public sealed partial class StructCollectionParityMapper :
    MapperDefinition<StructCollectionParitySource, StructCollectionParityTarget>
{
    public override void Configure(MappingContext<StructCollectionParitySource, StructCollectionParityTarget> context)
    {
        context.Map(source => source.Value, target => target.Value);
        context.Map(source => source.Items, target => target.Items);
    }
}
