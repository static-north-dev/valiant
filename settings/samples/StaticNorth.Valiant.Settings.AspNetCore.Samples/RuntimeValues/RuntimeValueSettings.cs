namespace StaticNorth.Valiant.Settings.AspNetCore.Samples.RuntimeValues;

[ValiantSettings("Samples:AspNetCore:RuntimeValues", configure: true)]
public sealed record RuntimeValueSettings
{
    public required string Name { get; init; }

    [ValiantSecret]
    public required string ApiKey { get; init; }

    public required string AccessToken { get; init; }
}
