using BenchmarkDotNet.Running;
using StaticNorth.Valiant.Validation.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(ValidationBenchmarks).Assembly).Run(args);
