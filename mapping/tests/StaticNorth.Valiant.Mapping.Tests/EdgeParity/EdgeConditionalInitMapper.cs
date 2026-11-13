namespace StaticNorth.Valiant.Mapping.Tests.EdgeParity;

[ValiantMapper]
public sealed partial class EdgeConditionalInitMapper : MapperDefinition<EdgeInitInput, EdgeInitOutput>
{
    public override void Configure(MappingContext<EdgeInitInput, EdgeInitOutput> context)
    {
        context.Map(source => source.Value, target => target.Value);
        context.Ignore(target => target.Value).When(source => source.Skip);
    }
}
