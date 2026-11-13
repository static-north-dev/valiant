using BenchmarkDotNet.Running;
using StaticNorth.Valiant.Validation.Benchmarks;

if (args is ["--runtime-async-kestrel-load", var runtimeAsyncAssembly])
{
    await RuntimeAsyncKestrelLoad.RunAsync(runtimeAsyncAssembly).ConfigureAwait(false);
    return;
}

if (args is ["--runtime-async-kestrel-server", var nested, var suspend, var runtimeAsync])
{
    await RuntimeAsyncKestrelLoad.ServeAsync(bool.Parse(nested), bool.Parse(suspend), bool.Parse(runtimeAsync))
        .ConfigureAwait(false);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(ValidationBenchmarks).Assembly).Run(args);
