namespace Valiant.Validation.Samples.AttributeValidation;

/// <summary>
/// Represents a sample user creation request.
/// </summary>
public sealed record CreateUserRequest
{
    [Required(Message = "User name is required.")]
    [MinLength(3)]
    [MaxLength(32)]
    public string? UserName { get; init; }

    [MaxLength(40)]
    public string? DisplayName { get; init; }

    [InclusiveBetween(0, 120)]
    public int Age { get; init; }

    public List<string> Tags { get; init; } = [];
}
