using Samples = Valiant.Settings.AspNetCore.Samples;

Type startup;

startup = typeof(Samples.Basic.Startup);
//startup = typeof(Samples.Configuration.Startup);
//startup = typeof(Samples.RuntimeValues.Startup);
//startup = typeof(Samples.SecretReveal.Startup);
//startup = typeof(Samples.Validation.Startup);
//startup = typeof(Samples.Validation.InvalidStartup);
//startup = typeof(Samples.Validation.RequiredStartup);

var host = Host.CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup(startup))
    .Build();

await host.RunAsync();
