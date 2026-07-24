namespace Valiant.Settings.Samples.SchemaMetadata;

public sealed record ReplicaSettings
{
    public required string Region { get; init; }

    public int Priority { get; init; }
}
