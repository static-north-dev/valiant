using StaticNorth.Valiant.Validation;

namespace StaticNorth.Valiant.Settings.AspNetCore.Samples.Validation;

[ValiantSettings("Samples:AspNetCore:Validation:Validated", configure: true)]
public sealed record ValidatedSettings
{
    [Required]
    public required string Name { get; init; }

    [GreaterThan(0)]
    public int RetryCount { get; init; }
}
