using System.Runtime.CompilerServices;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Perfolizer.Horology;

namespace StaticNorth.Valiant.Validation.Benchmarks;

public sealed class RuntimeAsyncConfig : ManualConfig
{
    private const string RuntimeAsyncEnvironmentVariable = "VALIANT_BENCHMARK_RUNTIME_ASYNC";

    public RuntimeAsyncConfig()
    {
        var job = Job.Default
            .WithLaunchCount(3)
            .WithWarmupCount(5)
            .WithIterationCount(12)
            .WithIterationTime(TimeInterval.FromMilliseconds(120));

        AddJob(job.WithId("RuntimeAsyncOff")
            .WithMsBuildArguments("/p:ValiantBenchmarkRuntimeAsync=false")
            .WithEnvironmentVariable(RuntimeAsyncEnvironmentVariable, "off")
            .AsBaseline());
        AddJob(job.WithId("RuntimeAsyncOn")
            .WithMsBuildArguments("/p:ValiantBenchmarkRuntimeAsync=true")
            .WithEnvironmentVariable(RuntimeAsyncEnvironmentVariable, "on"));
    }

    internal static void AssertValidatorLowering() =>
        AssertValidatorLowering(Environment.GetEnvironmentVariable(RuntimeAsyncEnvironmentVariable) == "on");

    internal static void AssertValidatorLowering(bool runtimeAsyncEnabled)
    {
        AssertLowering(typeof(RuntimeAsyncSingleValidator), runtimeAsyncEnabled);
        AssertLowering(typeof(RuntimeAsyncNestedValidator), runtimeAsyncEnabled);
    }

    private static void AssertLowering(Type validator, bool runtimeAsyncEnabled)
    {
        var method = validator.GetMethod(nameof(IValidator<RuntimeAsyncValidationInput>.ValidateAsync))!;
        var usesCompilerStateMachine = method.IsDefined(typeof(AsyncStateMachineAttribute), inherit: false);
        if (usesCompilerStateMachine == runtimeAsyncEnabled)
        {
            throw new InvalidOperationException($"Unexpected async lowering for {validator.Name}.");
        }
    }
}
