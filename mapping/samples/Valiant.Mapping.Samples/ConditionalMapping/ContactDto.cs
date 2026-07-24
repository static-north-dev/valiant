namespace Valiant.Mapping.Samples.ConditionalMapping;

public sealed record ContactDto
{
    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }
}
