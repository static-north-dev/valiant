using Microsoft.Extensions.Configuration;
using Valiant.Settings;
using Valiant.Settings.AspNetCore;
using Valiant.Settings.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["PackageSmoke:Message"] = "packaged settings",
});
builder.Services.AddValiantSettings(builder.Configuration);

var app = builder.Build();
app.MapValiantSettingsUi();

var schema = ValiantSettingsSchemas.Get<UiSmokeSettings>().Schema;
if (schema.SectionPath != "PackageSmoke")
{
    throw new InvalidOperationException("The ASP.NET Core leaf package did not forward the settings analyzer.");
}

Console.WriteLine("Valiant.Settings.AspNetCore package consumer passed.");

[ValiantSettings("PackageSmoke", configure: true)]
public sealed record UiSmokeSettings
{
    public string? Message { get; init; }
}
