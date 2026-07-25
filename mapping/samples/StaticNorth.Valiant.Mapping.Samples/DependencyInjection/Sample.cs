using Microsoft.Extensions.DependencyInjection;

namespace Valiant.Mapping.Samples.DependencyInjection;

/// <summary>
/// Demonstrates generated registration and the typed mapper, non-generic
/// facade, factory, successful <c>Try</c>, and missing-pair <c>Try</c> APIs.
/// </summary>
public static class Sample
{
    public static void Run()
    {
        var services = new ServiceCollection();
        services.AddValiantMappers(options =>
        {
            options.Mode = ValiantMappingMode.Auto;
            options.NullHandling = ValiantNullHandling.MapNulls;
        });

        using var provider = services.BuildServiceProvider();
        var source = new Person("Grace", 40);
        var typed = provider.GetRequiredService<IMapper<Person, PersonDto>>();
        var facade = provider.GetRequiredService<IMapper>();
        var factory = provider.GetRequiredService<IMapperFactory>();

        var typedTarget = typed.Map(source);
        var facadeTarget = facade.Map<PersonDto>(source);
        var facadeFound = facade.TryMap<PersonDto>(source, out var tryTarget);
        var facadeMissing = facade.TryMap<string>(source, out _);
        var factoryMapper = factory.CreateMapper<Person, PersonDto>();
        var factoryFound = factory.TryCreateMapper<Person, PersonDto>(out var tryMapper);
        var factoryMissing = factory.TryCreateMapper<Person, string>(out _);

        Console.WriteLine($"Typed mapper: {typedTarget.Name}");
        Console.WriteLine($"Facade: {facadeTarget.Name}; Try found: {facadeFound} ({tryTarget?.Name})");
        Console.WriteLine($"Factory: {factoryMapper.Map(source).Name}; Try found: {factoryFound} ({tryMapper?.Map(source).Name})");
        Console.WriteLine($"Missing facade/factory pairs: {facadeMissing}/{factoryMissing}");
    }
}
