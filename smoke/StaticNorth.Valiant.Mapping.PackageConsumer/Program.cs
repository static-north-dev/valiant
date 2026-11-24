using StaticNorth.Valiant.Mapping;

foreach (var mode in new[] { ValiantMappingMode.Fast, ValiantMappingMode.Debuggable })
{
    var target = new SmokeMapper(new ValiantMappingOptions { Mode = mode }).Map(new SmokeSource("packaged mapper"));
    if (target.Value != "packaged mapper")
    {
        throw new InvalidOperationException($"The packaged mapping generator did not produce a working {mode} mapper.");
    }
}

Console.WriteLine("StaticNorth.Valiant.Mapping package consumer passed.");

public sealed record SmokeSource(string Value);

public sealed record SmokeTarget(string? Value);

[ValiantMapper]
public sealed partial class SmokeMapper : MapperDefinition<SmokeSource, SmokeTarget>
{
    public override void Configure(MappingContext<SmokeSource, SmokeTarget> context)
    {
        context.Map(source => source.Value, target => target.Value);
    }
}
