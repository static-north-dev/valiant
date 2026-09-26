namespace StaticNorth.Valiant.Settings.Tests;

[ValiantSettings("SecurityTests")]
public sealed class SettingsSecurityModel
{
    [ValiantSecret]
    public string Credential { get; set; } = "sensitive-test-value";

    public SettingsSecurityModel? Child { get; set; }

    public object? Opaque { get; set; }

    public bool FailGetter { get; set; }

    [ValiantSecret]
    public string ThrowingSecret => FailGetter ? throw new InvalidOperationException(Credential) : Credential;
}
