using StaticNorth.Valiant.Validation;

namespace StaticNorth.Valiant.Settings.Tests;

[ValiantValidator]
public sealed partial class ModeParitySettingsValidator : ValidatorDefinition<ModeParitySettings>
{
    public override void Configure(ValidationContext<ModeParitySettings> context)
    {
        context.Property(settings => settings.Name).Required(message: "A name is required.");
        context.Property(settings => settings.Retries).GreaterThan(0, message: "Retries must be positive.");
        context.Property(settings => settings.Retries).LessThan(3, severity: ValiantSeverity.Warning);
    }
}
