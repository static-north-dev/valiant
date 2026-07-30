using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using StaticNorth.Valiant.Validation;

namespace StaticNorth.Valiant.Validation.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ValidationResultAccessBenchmarks
{
    private ValidationResult _result = null!;

    [GlobalSetup]
    public void Setup()
    {
        _result = new ValidationResult(
        [
            new ValidationFailure("Name", "Required", "'Name' is required."),
            new ValidationFailure("Username", "Warning", "'Username' is unusual.", ValiantSeverity.Warning),
            new ValidationFailure("CountryCode", "Info", "'CountryCode' was normalized.", ValiantSeverity.Info),
            new ValidationFailure("Age", "InclusiveBetween", "'Age' must be between 18 and 99."),
            new ValidationFailure("Nickname", "Warning", "'Nickname' is unusual.", ValiantSeverity.Warning)
        ]);
    }

    [Benchmark]
    public int IndexedPartitionAccess()
    {
        return _result.Errors[0].ErrorCode.Length
            + _result.Errors[1].ErrorCode.Length
            + _result.Warnings[0].ErrorCode.Length
            + _result.Warnings[1].ErrorCode.Length
            + _result.Infos[0].ErrorCode.Length;
    }

    [Benchmark]
    public int EnumeratePartitions()
    {
        var length = 0;
        foreach (var failure in _result.Errors)
        {
            length += failure.ErrorCode.Length;
        }

        foreach (var failure in _result.Warnings)
        {
            length += failure.ErrorCode.Length;
        }

        foreach (var failure in _result.Infos)
        {
            length += failure.ErrorCode.Length;
        }

        return length;
    }
}
