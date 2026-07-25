namespace Valiant.Settings.Samples.SchemaRegistry;

[ValiantSettings("Samples:SchemaRegistry")]
public sealed record RegistrySettings
{
    public bool Enabled { get; init; }
}
