using StaticNorth.Valiant.Validation;

namespace StaticNorth.Valiant.Settings.AspNetCore.Samples.Validation;

[ValiantValidator]
public sealed partial class ValidatedSettingsValidator : ValidatorDefinition<ValidatedSettings>;
