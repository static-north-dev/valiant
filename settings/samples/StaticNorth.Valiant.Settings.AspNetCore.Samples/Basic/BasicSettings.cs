namespace StaticNorth.Valiant.Settings.AspNetCore.Samples.Basic;

[ValiantSettings("Samples:AspNetCore:Basic")]
public sealed record BasicSettings
{
    public required string Message { get; init; }
}
