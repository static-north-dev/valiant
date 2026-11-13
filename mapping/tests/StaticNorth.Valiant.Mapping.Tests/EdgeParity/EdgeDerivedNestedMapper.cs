namespace StaticNorth.Valiant.Mapping.Tests.EdgeParity;

[ValiantMapper]
public sealed partial class EdgeDerivedNestedMapper : MapperDefinition<EdgeNestedInput, EdgeDerivedNestedOutput>
{
    private readonly IMapper<EdgeInner, EdgeInnerDto> _nested = new EdgeMultiMapper();

    public override void Configure(MappingContext<EdgeNestedInput, EdgeDerivedNestedOutput> context)
    {
        context.UseMapper(_nested);
        context.Map(source => source.Value, target => target.Value);
    }
}
