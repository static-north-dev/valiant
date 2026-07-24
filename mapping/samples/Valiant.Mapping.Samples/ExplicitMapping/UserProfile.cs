namespace Valiant.Mapping.Samples.ExplicitMapping;

public sealed record UserProfile(
    string FirstName,
    string LastName,
    string EmailAddress,
    string InternalNote);
