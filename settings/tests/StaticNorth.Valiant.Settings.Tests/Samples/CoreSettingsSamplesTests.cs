using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StaticNorth.Valiant.Settings.Samples.Basic;
using StaticNorth.Valiant.Settings.Samples.RegistrationOptions;
using StaticNorth.Valiant.Settings.Samples.SchemaMetadata;
using StaticNorth.Valiant.Settings.Samples.SchemaRegistry;

namespace StaticNorth.Valiant.Settings.Tests.Samples;

public sealed class CoreSettingsSamplesTests
{
    private static readonly Assembly SampleAssembly = typeof(BasicSettings).Assembly;

    [Test]
    public async Task Basic_Sample_Binds_Every_Configured_Settings_Model()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Samples:Basic:Message"] = "Hello from Valiant Settings",
                ["Samples:Db:ConnectionString"] = "Server=test;Database=sample;"
            })
            .Build();
        var services = new ServiceCollection();

        AddSampleSettings(services, configuration);

        using var provider = services.BuildServiceProvider();
        var basic = provider.GetRequiredService<IOptions<BasicSettings>>().Value;
        var database = provider.GetRequiredService<IOptions<DbSettings>>().Value;

        await Assert.That(basic.Message).IsEqualTo("Hello from Valiant Settings");
        await Assert.That(database.ConnectionString).IsEqualTo("Server=test;Database=sample;");
    }

    [Test]
    public async Task Registration_Options_Sample_Can_Disable_Providers_Without_Disabling_Binding()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Samples:Registration:Value"] = "bound without schema providers"
            })
            .Build();
        var defaultServices = new ServiceCollection();
        AddSampleSettings(defaultServices, configuration);
        var bindingOnlyServices = new ServiceCollection();
        AddSampleSettings(
            bindingOnlyServices,
            configuration,
            options => options.RegisterSchemaProviders = false);

        using var defaultProvider = defaultServices.BuildServiceProvider();
        using var bindingOnlyProvider = bindingOnlyServices.BuildServiceProvider();
        var expectedProviderCount = GetAllSchemas().Count;
        var defaultProviderCount = defaultProvider
            .GetServices<IValiantSettingsSchemaProvider>()
            .Count();
        var bindingOnlyProviderCount = bindingOnlyProvider
            .GetServices<IValiantSettingsSchemaProvider>()
            .Count();
        var settings = bindingOnlyProvider
            .GetRequiredService<IOptions<RegistrationSettings>>()
            .Value;

        await Assert.That(defaultProviderCount).IsEqualTo(expectedProviderCount);
        await Assert.That(bindingOnlyProviderCount).IsEqualTo(0);
        await Assert.That(settings.Value).IsEqualTo("bound without schema providers");
    }

    [Test]
    public async Task Schema_Registry_Sample_Covers_Enumeration_And_Typed_Lookups()
    {
        var all = GetAllSchemas();
        var provider = GetSchema<RegistrySettings>();
        var annotatedFound = TryGetSchema<RegistrySettings>(out var sameProvider);
        var unannotatedFound = TryGetSchema<UnregisteredSettings>(out var missingProvider);

        await Assert.That(all.Select(candidate => candidate.SettingsType))
            .Contains(typeof(RegistrySettings));
        await Assert.That(provider.SettingsType).IsEqualTo(typeof(RegistrySettings));
        await Assert.That(annotatedFound).IsTrue();
        await Assert.That(ReferenceEquals(provider, sameProvider)).IsTrue();
        await Assert.That(unannotatedFound).IsFalse();
        await Assert.That(missingProvider).IsNull();
    }

    [Test]
    public async Task Schema_Metadata_Sample_Covers_Every_Demonstrated_Shape()
    {
        var schema = GetSchema<SchemaMetadataSettings>().Schema;
        var properties = schema.Properties.ToDictionary(property => property.Name);

        await Assert.That(schema.SectionPath).IsEqualTo("Samples:SchemaMetadata");
        await AssertProperty(properties[nameof(SchemaMetadataSettings.Name)], ValiantSettingsPropertyKind.String, required: true, nullable: false);
        await AssertProperty(properties[nameof(SchemaMetadataSettings.Description)], ValiantSettingsPropertyKind.String, required: false, nullable: true);
        await AssertProperty(properties[nameof(SchemaMetadataSettings.Enabled)], ValiantSettingsPropertyKind.Boolean);
        await AssertProperty(properties[nameof(SchemaMetadataSettings.RetryCount)], ValiantSettingsPropertyKind.Integer);
        await AssertProperty(properties[nameof(SchemaMetadataSettings.FailureThreshold)], ValiantSettingsPropertyKind.Number);
        await AssertProperty(properties[nameof(SchemaMetadataSettings.Mode)], ValiantSettingsPropertyKind.Enum);
        await AssertProperty(properties[nameof(SchemaMetadataSettings.Primary)], ValiantSettingsPropertyKind.Object, required: true);
        await AssertProperty(properties[nameof(SchemaMetadataSettings.Tags)], ValiantSettingsPropertyKind.Array, required: true);
        await AssertProperty(properties[nameof(SchemaMetadataSettings.Replicas)], ValiantSettingsPropertyKind.Array, required: true);
        await AssertProperty(properties[nameof(SchemaMetadataSettings.HealthCheck)], ValiantSettingsPropertyKind.Unknown, required: false, nullable: true);

        var apiKey = properties[nameof(SchemaMetadataSettings.ApiKey)];
        var tags = properties[nameof(SchemaMetadataSettings.Tags)];
        var replicas = properties[nameof(SchemaMetadataSettings.Replicas)];
        var primary = properties[nameof(SchemaMetadataSettings.Primary)];
        var retryCount = properties[nameof(SchemaMetadataSettings.RetryCount)];

        await Assert.That(apiKey.IsSecret).IsTrue();
        await Assert.That(apiKey.IsRequired).IsTrue();
        await Assert.That(tags.Item?.Kind).IsEqualTo(ValiantSettingsPropertyKind.String);
        await Assert.That(replicas.Item?.Kind).IsEqualTo(ValiantSettingsPropertyKind.Object);
        await Assert.That(replicas.Item?.Properties.Select(property => property.Name))
            .IsEquivalentTo([nameof(ReplicaSettings.Region), nameof(ReplicaSettings.Priority)]);
        await Assert.That(primary.Properties.Select(property => property.Name))
            .IsEquivalentTo([nameof(ReplicaSettings.Region), nameof(ReplicaSettings.Priority)]);
        await Assert.That(retryCount.DefaultValue).IsNull();
    }

    private static void AddSampleSettings(
        IServiceCollection services,
        IConfiguration configuration,
        Action<ValiantSettingsOptions>? configure = null)
    {
        var extensionType = SampleAssembly.GetType(
            "StaticNorth.Valiant.Settings.DependencyInjection.ValiantSettingsServiceCollectionExtensions",
            throwOnError: true)!;
        var method = extensionType.GetMethod(
            "AddValiantSettings",
            [typeof(IServiceCollection), typeof(IConfiguration), typeof(Action<ValiantSettingsOptions>)])!;

        _ = method.Invoke(null, [services, configuration, configure]);
    }

    private static IReadOnlyList<IValiantSettingsSchemaProvider> GetAllSchemas()
    {
        return (IReadOnlyList<IValiantSettingsSchemaProvider>)GetRegistryType()
            .GetProperty("All")!
            .GetValue(null)!;
    }

    private static IValiantSettingsSchemaProvider GetSchema<TSettings>()
    {
        return (IValiantSettingsSchemaProvider)GetRegistryType()
            .GetMethod("Get")!
            .MakeGenericMethod(typeof(TSettings))
            .Invoke(null, null)!;
    }

    private static bool TryGetSchema<TSettings>(out IValiantSettingsSchemaProvider? provider)
    {
        object?[] arguments = [null];
        var found = (bool)GetRegistryType()
            .GetMethod("TryGet")!
            .MakeGenericMethod(typeof(TSettings))
            .Invoke(null, arguments)!;
        provider = (IValiantSettingsSchemaProvider?)arguments[0];
        return found;
    }

    private static Type GetRegistryType()
    {
        return SampleAssembly.GetType("StaticNorth.Valiant.Settings.ValiantSettingsSchemas", throwOnError: true)!;
    }

    private static async Task AssertProperty(
        ValiantSettingsPropertySchema property,
        ValiantSettingsPropertyKind kind,
        bool required = false,
        bool nullable = false)
    {
        await Assert.That(property.Kind).IsEqualTo(kind);
        await Assert.That(property.IsRequired).IsEqualTo(required);
        await Assert.That(property.IsNullable).IsEqualTo(nullable);
    }

    private sealed record UnregisteredSettings;
}
