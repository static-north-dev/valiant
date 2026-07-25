namespace Valiant.Settings.Samples.SchemaRegistry;

/// <summary>
/// Demonstrates generated schema registry enumeration and typed lookups.
/// </summary>
public static class Sample
{
    public static void Run()
    {
        var provider = ValiantSettingsSchemas.Get<RegistrySettings>();
        var annotatedModelWasFound =
            ValiantSettingsSchemas.TryGet<RegistrySettings>(out var sameProvider);
        var unannotatedModelWasFound =
            ValiantSettingsSchemas.TryGet<UnregisteredSettings>(out _);

        Console.WriteLine($"Schemas generated in this project: {ValiantSettingsSchemas.All.Count}");
        Console.WriteLine($"Get returned: {provider.Schema.Name}");
        Console.WriteLine(
            $"TryGet found the annotated model: " +
            $"{annotatedModelWasFound && ReferenceEquals(provider, sameProvider)}");
        Console.WriteLine($"TryGet found an unannotated model: {unannotatedModelWasFound}");
    }

    private sealed record UnregisteredSettings;
}
