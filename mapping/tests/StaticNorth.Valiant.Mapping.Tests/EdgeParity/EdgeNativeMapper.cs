namespace StaticNorth.Valiant.Mapping.Tests.EdgeParity;

[ValiantMapper]
public sealed partial class EdgeNativeMapper : MapperDefinition<EdgeNativeInput, EdgeNativeOutput>
{
    public override void Configure(MappingContext<EdgeNativeInput, EdgeNativeOutput> context)
    {
        context.Map(source => source.Value, target => target.Value);
        context.Map(source => source.Count, target => target.Count);
        context.Map(source => source.Widen, target => target.Widen);
    }
}
