namespace Valiant.Settings.Samples.SchemaMetadata;

/// <summary>
/// Demonstrates generated property kinds, nesting, collections, nullability, and secret metadata.
/// </summary>
public static class Sample
{
    public static void Run()
    {
        var schema = ValiantSettingsSchemas.Get<SchemaMetadataSettings>().Schema;

        Console.WriteLine($"{schema.Name} -> {schema.SectionPath}");
        foreach (var property in schema.Properties)
        {
            var item = property.Item is null
                ? ""
                : $", item: {property.Item.Kind}";
            Console.WriteLine(
                $"  {property.ConfigurationKey}: {property.Kind}, " +
                $"required: {property.IsRequired}, nullable: {property.IsNullable}, " +
                $"secret: {property.IsSecret}{item}");
        }

        var retryCount = schema.Properties.Single(property =>
            property.Name == nameof(SchemaMetadataSettings.RetryCount));
        Console.WriteLine(
            $"Initializer metadata: {retryCount.DefaultValue ?? "not emitted (runtime initializer only)"}");
    }
}
