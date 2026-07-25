namespace Valiant.Settings.AspNetCore.Samples.SecretReveal;

[ValiantSettings("Samples:AspNetCore:SecretReveal", configure: true)]
public sealed record SecretRevealSettings
{
    public required string Name { get; init; }

    [ValiantSecret]
    public required string ApiKey { get; init; }
}
