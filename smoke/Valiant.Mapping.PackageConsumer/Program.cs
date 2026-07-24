using Valiant.Mapping;

var target = new SmokeMapper(new ValiantMappingOptions
{
    Mode = ValiantMappingMode.Fast,
}).Map(new SmokeSource("packaged mapper"));

if (target.Value != "packaged mapper")
{
    throw new InvalidOperationException("The packaged mapping generator did not produce a working fast mapper.");
}

Console.WriteLine("Valiant.Mapping package consumer passed.");

public sealed record SmokeSource(string Value);

public sealed record SmokeTarget(string? Value);

[ValiantMapper]
public sealed partial class SmokeMapper : MapperDefinition<SmokeSource, SmokeTarget>;
