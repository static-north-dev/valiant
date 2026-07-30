using BenchmarkDotNet.Running;
using StaticNorth.Valiant.Mapping.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(FlatMappingBenchmarks).Assembly).Run(args);
