namespace StaticNorth.Valiant.Mapping.Tests;

public sealed class CollectionMappingOptimizationTests
{
    [Test]
    public async Task Fast_Mapping_Uses_Array_Set_And_SinglePass_Enumerable_Shapes()
    {
        var oneShot = new SinglePassEnumerable<CollectionItemSource>(
        [
            new CollectionItemSource { Value = 5 },
            new CollectionItemSource { Value = 8 }
        ]);
        var firstSetItem = new CollectionItemSource { Value = 13 };
        var source = new CollectionShapeSource
        {
            ArrayItems =
            [
                new CollectionItemSource { Value = 1 },
                new CollectionItemSource { Value = 2 }
            ],
            SetItems = [firstSetItem, firstSetItem, new CollectionItemSource { Value = 21 }],
            StreamItems = oneShot
        };
        var options = new ValiantMappingOptions { Mode = ValiantMappingMode.Fast };
        var mapper = new CollectionShapeMapper(new CollectionItemMapper(options), options);

        var target = mapper.Map(source);

        await Assert.That(target.ArrayItems.Select(static item => item.Value))
            .IsEquivalentTo([1, 2]);
        await Assert.That(target.SetItems.Select(static item => item.Value))
            .IsEquivalentTo([13, 21]);
        await Assert.That(target.StreamItems.Select(static item => item.Value))
            .IsEquivalentTo([5, 8]);
        await Assert.That(oneShot.EnumerationCount).IsEqualTo(1);
    }
}

public sealed class CollectionShapeSource
{
    public CollectionItemSource[] ArrayItems { get; init; } = [];

    public HashSet<CollectionItemSource> SetItems { get; init; } = [];

    public IEnumerable<CollectionItemSource> StreamItems { get; init; } = [];
}

public sealed class CollectionShapeTarget
{
    public CollectionItemTarget[] ArrayItems { get; set; } = [];

    public HashSet<CollectionItemTarget> SetItems { get; set; } = [];

    public CollectionItemTarget[] StreamItems { get; set; } = [];
}

public sealed class CollectionItemSource
{
    public int Value { get; init; }
}

public sealed class CollectionItemTarget
{
    public int Value { get; init; }
}

[ValiantMapper]
public sealed partial class CollectionItemMapper
    : MapperDefinition<CollectionItemSource, CollectionItemTarget>;

[ValiantMapper]
public sealed partial class CollectionShapeMapper(CollectionItemMapper itemMapper)
    : MapperDefinition<CollectionShapeSource, CollectionShapeTarget>
{
    public override void Configure(MappingContext<CollectionShapeSource, CollectionShapeTarget> context)
    {
        context.UseMapper(itemMapper);
        context.Map(source => source.ArrayItems, target => target.ArrayItems);
        context.Map(source => source.SetItems, target => target.SetItems);
        context.Map(source => source.StreamItems, target => target.StreamItems);
    }
}

public sealed class SinglePassEnumerable<T>(IEnumerable<T> values) : IEnumerable<T>
{
    private readonly IEnumerable<T> _values = values;

    public int EnumerationCount { get; private set; }

    public IEnumerator<T> GetEnumerator()
    {
        EnumerationCount++;
        if (EnumerationCount > 1)
        {
            throw new InvalidOperationException("The sequence was enumerated more than once.");
        }

        return _values.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
