namespace Valiant.Settings.Samples.Basic;

[ValiantSettings("Samples:Basic", configure: true)]
public sealed record BasicSettings
{
    public required string Message { get; init; }
}
