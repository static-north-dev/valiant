using StaticNorth.Valiant.Mapping;
using StaticNorth.Valiant.Settings;
using StaticNorth.Valiant.Validation;

var mapped = new AotMapper(new ValiantMappingOptions
{
    Mode = ValiantMappingMode.Fast,
}).Map(new AotSource("native"));

var validation = new AotValidator(new ValiantValidationOptions
{
    Mode = ValiantValidationMode.Fast,
}).Validate(new AotRequest(""));

var schema = ValiantSettingsSchemas.Get<AotSettings>().Schema;

if (mapped.Value != "native" || validation.IsValid || schema.SectionPath != "Aot")
{
    return 1;
}

Console.WriteLine("Valiant Native AOT package smoke passed.");
return 0;

public sealed record AotSource(string Value);

public sealed record AotTarget(string? Value);

[ValiantMapper]
public sealed partial class AotMapper : MapperDefinition<AotSource, AotTarget>;

public sealed record AotRequest(string? Name);

[ValiantValidator]
public sealed partial class AotValidator : ValidatorDefinition<AotRequest>
{
    public override void Configure(ValidationContext<AotRequest> context)
    {
        context.Property(static request => request.Name).Required();
    }
}

[ValiantSettings("Aot")]
public sealed record AotSettings
{
    public string? Value { get; init; }
}
