using Samples = StaticNorth.Valiant.Validation.AspNetCore.Samples;

Type startup;

startup = typeof(Samples.MinimalApi.Startup);
//startup = typeof(Samples.Mvc.Startup);
//startup = typeof(Samples.ProblemDetails.Startup);
//startup = typeof(Samples.OpenApi.Startup);

var host = Host.CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup(startup))
    .Build();

await host.RunAsync();
