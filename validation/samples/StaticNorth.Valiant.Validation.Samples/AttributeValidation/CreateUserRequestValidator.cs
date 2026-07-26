namespace StaticNorth.Valiant.Validation.Samples.AttributeValidation;

/// <summary>
/// Validates sample user creation requests.
/// </summary>
[ValiantValidator]
public sealed partial class CreateUserRequestValidator : ValidatorDefinition<CreateUserRequest>;
