namespace StaticNorth.Valiant.Mapping.Tests;

[ValiantMapper]
public sealed partial class MappingModeParentMapper : MapperDefinition<MappingModeParentSource, MappingModeParentTarget>
{
    public override void Configure(MappingContext<MappingModeParentSource, MappingModeParentTarget> context)
    {
        if (context.Source.Probe.CaptureBackend())
        {
            context.Map(source => source.Child, target => target.Nested);
        }
    }
}
