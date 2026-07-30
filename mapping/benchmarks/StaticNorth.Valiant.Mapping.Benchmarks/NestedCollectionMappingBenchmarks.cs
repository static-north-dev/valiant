using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using StaticNorth.Valiant.Mapping;

namespace StaticNorth.Valiant.Mapping.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class NestedCollectionMappingBenchmarks
{
    private OrderMapperlyMapper _mapperlyMapper = null!;
    private OrderValiantMapper _valiantMapper = null!;
    private OrderSource _source = null!;

    [Params(0, 10, 100)]
    public int CollectionSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _mapperlyMapper = new OrderMapperlyMapper();
        var options = new ValiantMappingOptions
        {
            Mode = ValiantMappingMode.Fast,
        };
        var lineItemMapper = new LineItemValiantMapper(options);
        _valiantMapper = new OrderValiantMapper(lineItemMapper, options);
        _source = new OrderSource
        {
            Id = Guid.Parse("ae20e315-5709-4584-9223-32fed25c655d"),
            CustomerName = "Grace Hopper",
            PrimaryItem = CreateItem(-1),
            Items = Enumerable.Range(0, CollectionSize).Select(CreateItem).ToList(),
        };

        var expected = _mapperlyMapper.Map(_source);
        MappingAssertions.Equal(expected, _valiantMapper.Map(_source), nameof(ValiantFast));
    }

    [Benchmark(Baseline = true)]
    public OrderTarget Mapperly()
    {
        return _mapperlyMapper.Map(_source);
    }

    [Benchmark]
    public OrderTarget ValiantFast()
    {
        return _valiantMapper.Map(_source);
    }

    private static LineItemSource CreateItem(int index)
    {
        return new LineItemSource
        {
            Sku = $"SKU-{index:D4}",
            Quantity = index + 2,
            Price = 12.50m + index,
        };
    }
}
