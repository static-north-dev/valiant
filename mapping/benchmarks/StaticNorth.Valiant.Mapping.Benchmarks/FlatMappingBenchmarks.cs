using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using StaticNorth.Valiant.Mapping;

namespace StaticNorth.Valiant.Mapping.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class FlatMappingBenchmarks
{
    private FlatMapperlyMapper _mapperlyMapper = null!;
    private FlatValiantMapper _valiantMapper = null!;
    private FlatSource _source = null!;

    [GlobalSetup]
    public void Setup()
    {
        _mapperlyMapper = new FlatMapperlyMapper();
        _valiantMapper = new FlatValiantMapper(new ValiantMappingOptions
        {
            Mode = ValiantMappingMode.Fast,
        });
        _source = new FlatSource
        {
            Id = Guid.Parse("64afcb6f-eed2-4f7d-87d7-ac9ea44a160c"),
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Age = 36,
            IsActive = true,
            Balance = 1843.25m,
            CreatedAt = new DateTimeOffset(2026, 7, 30, 8, 15, 0, TimeSpan.Zero),
            Score = 98.75,
        };

        var expected = _mapperlyMapper.Map(_source);
        MappingAssertions.Equal(expected, _valiantMapper.Map(_source), nameof(ValiantFast));
    }

    [Benchmark(Baseline = true)]
    public FlatTarget Mapperly()
    {
        return _mapperlyMapper.Map(_source);
    }

    [Benchmark]
    public FlatTarget ValiantFast()
    {
        return _valiantMapper.Map(_source);
    }
}
