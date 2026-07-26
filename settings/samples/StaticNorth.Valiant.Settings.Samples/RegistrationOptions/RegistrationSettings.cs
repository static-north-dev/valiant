namespace StaticNorth.Valiant.Settings.Samples.RegistrationOptions;

[ValiantSettings("Samples:Registration", configure: true)]
public sealed record RegistrationSettings
{
    public required string Value { get; init; }
}
