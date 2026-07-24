using Valiant.Validation;

namespace Valiant.Settings.AspNetCore.Samples.Validation;

[ValiantValidator]
public sealed partial class ValidatedSettingsValidator : ValidatorDefinition<ValidatedSettings>;
