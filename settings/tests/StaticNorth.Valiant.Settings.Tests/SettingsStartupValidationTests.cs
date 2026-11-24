using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StaticNorth.Valiant.Settings.Validation;
using StaticNorth.Valiant.Validation;

namespace StaticNorth.Valiant.Settings.Tests;

public sealed class SettingsStartupValidationTests
{
    [Test]
    public async Task Startup_Awaits_Valiant_Validation_And_Caches_Valid_Options()
    {
        var probe = new StartupValidationProbe();
        using var host = CreateHost("available", probe);

        await host.StartAsync();

        var options = host.Services.GetRequiredService<IOptions<StartupSettings>>().Value;
        await Assert.That(options.Endpoint).IsEqualTo("available");
        await Assert.That(probe.ValidationCalls).IsEqualTo(1);
        await Assert.That(probe.DisposalCalls).IsEqualTo(1);
        await host.StopAsync();
    }

    [Test]
    public async Task Startup_Reports_Valiant_Failures_As_Options_Validation_Errors()
    {
        var probe = new StartupValidationProbe();
        using var host = CreateHost("unavailable", probe);

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        await Assert.That(exception!.OptionsType).IsEqualTo(typeof(StartupSettings));
        await Assert.That(exception.Failures).IsEquivalentTo(["Endpoint: Endpoint is unavailable."]);
        await Assert.That(probe.ValidationCalls).IsEqualTo(1);
        await Assert.That(probe.DisposalCalls).IsEqualTo(1);
    }

    private static IHost CreateHost(string endpoint, StartupValidationProbe probe)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(probe);
        builder.Services.AddScoped<IValidator<StartupSettings>, AsyncStartupValidator>();
        builder.Services.AddValiantSettingsOptionsValidation<StartupSettings>(new ValiantSettingsValidationOptions
        {
            ValidateWithValiant = true,
            RequireValidators = true
        });
        builder.Services
            .AddOptions<StartupSettings>()
            .Configure(settings => settings.Endpoint = endpoint)
            .ValidateOnStart();

        return builder.Build();
    }

    private sealed class StartupSettings
    {
        public string? Endpoint { get; set; }
    }

    private sealed class StartupValidationProbe
    {
        public int ValidationCalls { get; set; }

        public int DisposalCalls { get; set; }
    }

    private sealed class AsyncStartupValidator(StartupValidationProbe probe) : IValidator<StartupSettings>, IAsyncDisposable
    {
        public ValidationResult Validate(StartupSettings instance)
        {
            throw new InvalidOperationException("This validator requires asynchronous validation.");
        }

        public async ValueTask<ValidationResult> ValidateAsync(StartupSettings instance, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            probe.ValidationCalls++;

            return instance.Endpoint == "available"
                ? ValidationResult.Valid
                : new ValidationResult([new ValidationFailure(nameof(StartupSettings.Endpoint), "Unavailable", "Endpoint is unavailable.")]);
        }

        public bool IsValid(StartupSettings instance)
        {
            return Validate(instance).IsValid;
        }

        public async ValueTask<bool> IsValidAsync(StartupSettings instance, CancellationToken cancellationToken = default)
        {
            return (await ValidateAsync(instance, cancellationToken)).IsValid;
        }

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            probe.DisposalCalls++;
        }
    }
}
