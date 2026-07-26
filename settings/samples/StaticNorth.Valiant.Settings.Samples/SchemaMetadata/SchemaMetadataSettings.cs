namespace StaticNorth.Valiant.Settings.Samples.SchemaMetadata;

[ValiantSettings("Samples:SchemaMetadata")]
public sealed record SchemaMetadataSettings
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public bool Enabled { get; init; }

    public int RetryCount { get; init; } = 3;

    public decimal FailureThreshold { get; init; } = 0.5m;

    public DeploymentMode Mode { get; init; }

    public ReplicaSettings Primary { get; init; } = new()
    {
        Region = ""
    };

    public string[] Tags { get; init; } = [];

    public ReplicaSettings[] Replicas { get; init; } = [];

    public Uri? HealthCheck { get; init; }

    [ValiantSecret]
    public string ApiKey { get; init; } = "";
}
