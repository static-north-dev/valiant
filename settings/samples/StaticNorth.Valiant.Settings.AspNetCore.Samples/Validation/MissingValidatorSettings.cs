namespace StaticNorth.Valiant.Settings.AspNetCore.Samples.Validation;

[ValiantSettings("Samples:AspNetCore:Validation:Missing", configure: true)]
public sealed record MissingValidatorSettings
{
    public bool Enabled { get; init; }
}
