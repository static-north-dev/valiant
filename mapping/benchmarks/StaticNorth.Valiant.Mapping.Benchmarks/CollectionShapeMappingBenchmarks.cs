using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using StaticNorth.Valiant.Mapping;

namespace StaticNorth.Valiant.Mapping.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ArrayCollectionMappingBenchmarks
{
    private ArrayCollectionMapperlyMapper _mapperly = null!;
    private ArrayCollectionValiantMapper _valiant = null!;
    private ArrayCollectionSource _source = null!;

    [Params(0, 10, 100)]
    public int CollectionSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var items = Enumerable.Range(0, CollectionSize).Select(CreateItem).ToArray();
        _mapperly = new ArrayCollectionMapperlyMapper();
        var options = new ValiantMappingOptions { Mode = ValiantMappingMode.Fast };
        _valiant = new ArrayCollectionValiantMapper(new LineItemValiantMapper(options), options);
        _source = new ArrayCollectionSource { Items = items };

        MappingAssertions.Equal(
            _mapperly.Map(_source).Items,
            _valiant.Map(_source).Items,
            nameof(ValiantArray),
            preserveOrder: true);
    }

    [Benchmark(Baseline = true)]
    public ArrayCollectionTarget MapperlyArray() => _mapperly.Map(_source);

    [Benchmark]
    public ArrayCollectionTarget ValiantArray() => _valiant.Map(_source);

    private static LineItemSource CreateItem(int index) => new()
    {
        Sku = $"SKU-{index:D4}",
        Quantity = index + 2,
        Price = 12.50m + index,
    };
}

[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class SetCollectionMappingBenchmarks
{
    private SetCollectionMapperlyMapper _mapperly = null!;
    private SetCollectionValiantMapper _valiant = null!;
    private SetCollectionSource _source = null!;

    [Params(0, 10, 100)]
    public int CollectionSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var items = Enumerable.Range(0, CollectionSize)
            .Select(static index => new LineItemSource
            {
                Sku = $"SKU-{index:D4}",
                Quantity = index + 2,
                Price = 12.50m + index,
            })
            .ToHashSet();
        _mapperly = new SetCollectionMapperlyMapper();
        var options = new ValiantMappingOptions { Mode = ValiantMappingMode.Fast };
        _valiant = new SetCollectionValiantMapper(new LineItemValiantMapper(options), options);
        _source = new SetCollectionSource { Items = items };

        MappingAssertions.Equal(
            _mapperly.Map(_source).Items,
            _valiant.Map(_source).Items,
            nameof(ValiantSet),
            preserveOrder: false);
    }

    [Benchmark(Baseline = true)]
    public SetCollectionTarget MapperlySet() => _mapperly.Map(_source);

    [Benchmark]
    public SetCollectionTarget ValiantSet() => _valiant.Map(_source);
}

[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class EnumerableCollectionMappingBenchmarks
{
    private EnumerableCollectionMapperlyMapper _mapperly = null!;
    private EnumerableCollectionValiantMapper _valiant = null!;
    private EnumerableCollectionSource _source = null!;

    [Params(0, 10, 100)]
    public int CollectionSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        IEnumerable<LineItemSource> items = Enumerable.Range(0, CollectionSize)
            .Select(static index => new LineItemSource
            {
                Sku = $"SKU-{index:D4}",
                Quantity = index + 2,
                Price = 12.50m + index,
            })
            .ToArray();
        _mapperly = new EnumerableCollectionMapperlyMapper();
        var options = new ValiantMappingOptions { Mode = ValiantMappingMode.Fast };
        _valiant = new EnumerableCollectionValiantMapper(new LineItemValiantMapper(options), options);
        _source = new EnumerableCollectionSource { Items = items };

        MappingAssertions.Equal(
            _mapperly.Map(_source).Items,
            _valiant.Map(_source).Items,
            nameof(ValiantEnumerable),
            preserveOrder: true);
    }

    [Benchmark(Baseline = true)]
    public EnumerableCollectionTarget MapperlyEnumerable() => _mapperly.Map(_source);

    [Benchmark]
    public EnumerableCollectionTarget ValiantEnumerable() => _valiant.Map(_source);
}
