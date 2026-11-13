namespace StaticNorth.Valiant.Settings.Tests;

[ValiantSettings("Parity", configure: true)]
public sealed class ModeParitySettings
{
    public string? Name { get; set; }

    public int Retries { get; set; }
}
