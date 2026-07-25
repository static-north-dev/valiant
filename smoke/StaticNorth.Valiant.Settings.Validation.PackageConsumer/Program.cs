using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Valiant.Settings;
using Valiant.Settings.DependencyInjection;
using Valiant.Validation;
using Valiant.Validation.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Validated:Name"] = "",
    })
    .Build();

var services = new ServiceCollection();
services.AddValiantValidators();
services.AddValiantSettings(configuration, options =>
{
    options.ValidateWithValiant = true;
    options.RequireValidators = true;
});

using var provider = services.BuildServiceProvider();
try
{
    _ = provider.GetRequiredService<IOptions<ValidatedSmokeSettings>>().Value;
    throw new InvalidOperationException("Packaged settings validation accepted invalid options.");
}
catch (OptionsValidationException)
{
}

Console.WriteLine("Valiant.Settings.Validation package consumer passed.");

[ValiantSettings("Validated", configure: true)]
public sealed record ValidatedSmokeSettings
{
    public string? Name { get; init; }
}

[ValiantValidator]
public sealed partial class ValidatedSmokeSettingsValidator : ValidatorDefinition<ValidatedSmokeSettings>
{
    public override void Configure(ValidationContext<ValidatedSmokeSettings> context)
    {
        context.Property(static settings => settings.Name).Required();
    }
}
