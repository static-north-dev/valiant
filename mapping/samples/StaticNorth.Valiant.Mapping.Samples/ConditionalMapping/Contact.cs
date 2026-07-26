namespace StaticNorth.Valiant.Mapping.Samples.ConditionalMapping;

public sealed record Contact
{
    public bool UseEmailAsDisplayName { get; init; }

    public bool IncludeContactDetails { get; init; }

    public string Name { get; init; } = "";

    public string Email { get; init; } = "";

    public string Phone { get; init; } = "";
}
