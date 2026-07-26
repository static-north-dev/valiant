namespace StaticNorth.Valiant.Settings.AspNetCore.Samples.Configuration;

[ValiantSettings("Samples:AspNetCore:Configuration")]
public sealed record ConfigurationSettings
{
    public bool Enabled { get; init; }
}
