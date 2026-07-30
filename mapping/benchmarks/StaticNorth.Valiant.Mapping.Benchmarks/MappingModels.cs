using StaticNorth.Valiant.Mapping;

namespace StaticNorth.Valiant.Mapping.Benchmarks;

public sealed class FlatSource
{
    public Guid Id { get; init; }

    public string Name { get; init; } = "";

    public string Email { get; init; } = "";

    public int Age { get; init; }

    public bool IsActive { get; init; }

    public decimal Balance { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public double Score { get; init; }
}

public sealed class FlatTarget
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    public int Age { get; set; }

    public bool IsActive { get; set; }

    public decimal Balance { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public double Score { get; set; }
}

[ValiantMapper]
public sealed partial class FlatValiantMapper : MapperDefinition<FlatSource, FlatTarget>;

[Riok.Mapperly.Abstractions.Mapper]
public sealed partial class FlatMapperlyMapper
{
    public partial FlatTarget Map(FlatSource source);
}

public sealed class OrderSource
{
    public Guid Id { get; init; }

    public string CustomerName { get; init; } = "";

    public LineItemSource PrimaryItem { get; init; } = new();

    public List<LineItemSource> Items { get; init; } = [];
}

public sealed class OrderTarget
{
    public Guid Id { get; set; }

    public string CustomerName { get; set; } = "";

    public LineItemTarget PrimaryItem { get; set; } = new();

    public List<LineItemTarget> Items { get; set; } = [];
}

public sealed class LineItemSource
{
    public string Sku { get; init; } = "";

    public int Quantity { get; init; }

    public decimal Price { get; init; }
}

public sealed class LineItemTarget
{
    public string Sku { get; set; } = "";

    public int Quantity { get; set; }

    public decimal Price { get; set; }
}

[ValiantMapper]
public sealed partial class LineItemValiantMapper : MapperDefinition<LineItemSource, LineItemTarget>;

[ValiantMapper]
public sealed partial class OrderValiantMapper(LineItemValiantMapper lineItemMapper)
    : MapperDefinition<OrderSource, OrderTarget>
{
    public override void Configure(MappingContext<OrderSource, OrderTarget> context)
    {
        context.UseMapper(lineItemMapper);
        context.Map(source => source.Id, target => target.Id);
        context.Map(source => source.CustomerName, target => target.CustomerName);
        context.Map(source => source.PrimaryItem, target => target.PrimaryItem);
        context.Map(source => source.Items, target => target.Items);
    }
}

[Riok.Mapperly.Abstractions.Mapper]
public sealed partial class OrderMapperlyMapper
{
    public partial OrderTarget Map(OrderSource source);

    private partial LineItemTarget MapLineItem(LineItemSource source);
}

public sealed class ArrayCollectionSource
{
    public LineItemSource[] Items { get; init; } = [];
}

public sealed class ArrayCollectionTarget
{
    public LineItemTarget[] Items { get; set; } = [];
}

[ValiantMapper]
public sealed partial class ArrayCollectionValiantMapper(LineItemValiantMapper lineItemMapper)
    : MapperDefinition<ArrayCollectionSource, ArrayCollectionTarget>
{
    public override void Configure(MappingContext<ArrayCollectionSource, ArrayCollectionTarget> context)
    {
        context.UseMapper(lineItemMapper);
        context.Map(source => source.Items, target => target.Items);
    }
}

[Riok.Mapperly.Abstractions.Mapper]
public sealed partial class ArrayCollectionMapperlyMapper
{
    public partial ArrayCollectionTarget Map(ArrayCollectionSource source);

    private partial LineItemTarget MapLineItem(LineItemSource source);
}

public sealed class SetCollectionSource
{
    public HashSet<LineItemSource> Items { get; init; } = [];
}

public sealed class SetCollectionTarget
{
    public HashSet<LineItemTarget> Items { get; set; } = [];
}

[ValiantMapper]
public sealed partial class SetCollectionValiantMapper(LineItemValiantMapper lineItemMapper)
    : MapperDefinition<SetCollectionSource, SetCollectionTarget>
{
    public override void Configure(MappingContext<SetCollectionSource, SetCollectionTarget> context)
    {
        context.UseMapper(lineItemMapper);
        context.Map(source => source.Items, target => target.Items);
    }
}

[Riok.Mapperly.Abstractions.Mapper]
public sealed partial class SetCollectionMapperlyMapper
{
    public partial SetCollectionTarget Map(SetCollectionSource source);

    private partial LineItemTarget MapLineItem(LineItemSource source);
}

public sealed class EnumerableCollectionSource
{
    public IEnumerable<LineItemSource> Items { get; init; } = [];
}

public sealed class EnumerableCollectionTarget
{
    public LineItemTarget[] Items { get; set; } = [];
}

[ValiantMapper]
public sealed partial class EnumerableCollectionValiantMapper(LineItemValiantMapper lineItemMapper)
    : MapperDefinition<EnumerableCollectionSource, EnumerableCollectionTarget>
{
    public override void Configure(MappingContext<EnumerableCollectionSource, EnumerableCollectionTarget> context)
    {
        context.UseMapper(lineItemMapper);
        context.Map(source => source.Items, target => target.Items);
    }
}

[Riok.Mapperly.Abstractions.Mapper]
public sealed partial class EnumerableCollectionMapperlyMapper
{
    public partial EnumerableCollectionTarget Map(EnumerableCollectionSource source);

    private partial LineItemTarget MapLineItem(LineItemSource source);
}
