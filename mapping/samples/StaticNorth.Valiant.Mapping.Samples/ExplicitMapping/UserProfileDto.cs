namespace StaticNorth.Valiant.Mapping.Samples.ExplicitMapping;

public sealed record UserProfileDto
{
    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    public string InternalNote { get; set; } = "not exposed";
}
