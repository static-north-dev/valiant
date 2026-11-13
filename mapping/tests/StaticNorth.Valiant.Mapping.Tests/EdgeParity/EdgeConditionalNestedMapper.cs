namespace StaticNorth.Valiant.Mapping.Tests.EdgeParity;

[ValiantMapper]
public sealed partial class EdgeConditionalNestedMapper : MapperDefinition<EdgeNestedInput, EdgeNestedOutput>
{
    private readonly EdgePrefixMapper _first = new("first:");
    private readonly EdgePrefixMapper _second = new("second:");

    public override void Configure(MappingContext<EdgeNestedInput, EdgeNestedOutput> context)
    {
        if (context.Source.UseFirst)
        {
            context.UseMapper(_first);
        }
        else
        {
            context.UseMapper(_second);
        }

        context.Map(source => source.Value, target => target.Value);
        context.Map(source => source.Groups, target => target.Groups);
    }
}
