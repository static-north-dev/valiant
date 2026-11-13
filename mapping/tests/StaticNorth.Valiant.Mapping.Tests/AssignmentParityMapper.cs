namespace StaticNorth.Valiant.Mapping.Tests;

[ValiantMapper]
public sealed partial class AssignmentParityMapper : MapperDefinition<AssignmentParitySource, AssignmentParityTarget>
{
    public override void Configure(MappingContext<AssignmentParitySource, AssignmentParityTarget> context)
    {
        context.Map(source => source.Read("second"), target => target.Second)
            .IgnoreNull()
            .Fallback(context.Source.Fallback());
        context.Map(source => source.Read("init"), target => target.Initial);
        context.Ignore(target => target.Initial).When(source => source.SkipInit);
        if (context.Source.UsePrimaryFallback())
        {
            context.Map(source => source.Read("first"), target => target.First)
                .MapNull()
                .Default(context.Source.RecordFallback("primary"));
        }
        else
        {
            context.Map(source => source.Read("first"), target => target.First)
                .MapNull()
                .Default(context.Source.RecordFallback("secondary"));
        }
    }
}
