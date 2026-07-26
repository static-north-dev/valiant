using Samples = StaticNorth.Valiant.Endpoints.AspNetCore.Samples;

Type? startup;

//startup = typeof(Samples.Basic.Startup);
//startup = typeof(Samples.Configuration.Startup);
//startup = typeof(Samples.ConditionalRegistration.Startup);
startup = typeof(Samples.QueryBinding.Startup);

var host = Host.CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup(startup))
    .Build();

await host.RunAsync();
