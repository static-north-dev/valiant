using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using BasicPerson = StaticNorth.Valiant.Mapping.Samples.Basic.Person;
using BasicPersonDto = StaticNorth.Valiant.Mapping.Samples.Basic.PersonDto;
using BasicPersonMapper = StaticNorth.Valiant.Mapping.Samples.Basic.PersonMapper;
using CompositionAddress = StaticNorth.Valiant.Mapping.Samples.Composition.Address;
using CompositionAddressMapper = StaticNorth.Valiant.Mapping.Samples.Composition.AddressMapper;
using CompositionCustomer = StaticNorth.Valiant.Mapping.Samples.Composition.Customer;
using CompositionCustomerMapper = StaticNorth.Valiant.Mapping.Samples.Composition.CustomerMapper;
using ConditionalContact = StaticNorth.Valiant.Mapping.Samples.ConditionalMapping.Contact;
using ConditionalContactMapper = StaticNorth.Valiant.Mapping.Samples.ConditionalMapping.ContactMapper;
using DependencyPerson = StaticNorth.Valiant.Mapping.Samples.DependencyInjection.Person;
using DependencyPersonDto = StaticNorth.Valiant.Mapping.Samples.DependencyInjection.PersonDto;
using DependencyPersonMapper = StaticNorth.Valiant.Mapping.Samples.DependencyInjection.PersonMapper;
using ExplicitUserProfile = StaticNorth.Valiant.Mapping.Samples.ExplicitMapping.UserProfile;
using ExplicitUserProfileMapper = StaticNorth.Valiant.Mapping.Samples.ExplicitMapping.UserProfileMapper;
using FlatteningOffice = StaticNorth.Valiant.Mapping.Samples.FlatteningAndEnums.Office;
using FlatteningOfficeImport = StaticNorth.Valiant.Mapping.Samples.FlatteningAndEnums.OfficeImport;
using FlatteningOfficeImportMapper = StaticNorth.Valiant.Mapping.Samples.FlatteningAndEnums.OfficeImportMapper;
using FlatteningOfficeLocation = StaticNorth.Valiant.Mapping.Samples.FlatteningAndEnums.OfficeLocation;
using FlatteningOfficeMapper = StaticNorth.Valiant.Mapping.Samples.FlatteningAndEnums.OfficeMapper;
using FlatteningOfficeStatus = StaticNorth.Valiant.Mapping.Samples.FlatteningAndEnums.OfficeStatus;
using FlatteningOfficeStatusDto = StaticNorth.Valiant.Mapping.Samples.FlatteningAndEnums.OfficeStatusDto;
using NullProfile = StaticNorth.Valiant.Mapping.Samples.NullHandling.Profile;
using NullProfileMapper = StaticNorth.Valiant.Mapping.Samples.NullHandling.ProfileMapper;

namespace StaticNorth.Valiant.Mapping.Tests;

public sealed class MappingSampleTests
{
    [Test]
    public void Every_Selectable_Sample_Entry_Point_Runs()
    {
        StaticNorth.Valiant.Mapping.Samples.Basic.Sample.Run();
        StaticNorth.Valiant.Mapping.Samples.ExplicitMapping.Sample.Run();
        StaticNorth.Valiant.Mapping.Samples.ConditionalMapping.Sample.Run();
        StaticNorth.Valiant.Mapping.Samples.NullHandling.Sample.Run();
        StaticNorth.Valiant.Mapping.Samples.FlatteningAndEnums.Sample.Run();
        StaticNorth.Valiant.Mapping.Samples.Composition.Sample.Run();
        StaticNorth.Valiant.Mapping.Samples.DependencyInjection.Sample.Run();
    }

    [Test]
    public async Task Basic_Sample_Maps_Matching_Members_Into_Immutable_Target()
    {
        BasicPersonDto target = new BasicPersonMapper().Map(new BasicPerson("Ada", 36));

        await Assert.That(target.Name).IsEqualTo("Ada");
        await Assert.That(target.Age).IsEqualTo(36);
    }

    [Test]
    public async Task Explicit_Mapping_Sample_Transforms_Renames_And_Ignores()
    {
        var target = new ExplicitUserProfileMapper().Map(
            new ExplicitUserProfile("Ada", "Lovelace", "ada@example.test", "private"));

        await Assert.That(target.DisplayName).IsEqualTo("Ada Lovelace");
        await Assert.That(target.Email).IsEqualTo("ada@example.test");
        await Assert.That(target.InternalNote).IsEqualTo("not exposed");
    }

    [Test]
    public async Task Conditional_Mapping_Sample_Covers_Native_Branches_Chained_When_And_Fallbacks()
    {
        var mapper = new ConditionalContactMapper();
        var hidden = mapper.Map(new ConditionalContact
        {
            Name = "Ada",
            Email = "ada@example.test",
            Phone = "+371 555 0100"
        });
        var visible = mapper.Map(new ConditionalContact
        {
            UseEmailAsDisplayName = true,
            IncludeContactDetails = true,
            Name = "Ada",
            Email = "ada@example.test",
            Phone = "+371 555 0100"
        });

        await Assert.That(hidden.DisplayName).IsEqualTo("Ada");
        await Assert.That(hidden.Email).IsEqualTo("Private email");
        await Assert.That(hidden.Phone).IsEqualTo("Private phone");
        await Assert.That(visible.DisplayName).IsEqualTo("ada@example.test");
        await Assert.That(visible.Email).IsEqualTo("ada@example.test");
        await Assert.That(visible.Phone).IsEqualTo("+371 555 0100");
    }

    [Test]
    public async Task Null_Handling_Sample_Covers_Option_And_Per_Rule_Precedence()
    {
        var source = new NullProfile();
        var mapNulls = new NullProfileMapper(new ValiantMappingOptions
        {
            Mode = ValiantMappingMode.Fast,
            NullHandling = ValiantNullHandling.MapNulls
        }).Map(source);
        var ignoreNulls = new NullProfileMapper(new ValiantMappingOptions
        {
            Mode = ValiantMappingMode.Fast,
            NullHandling = ValiantNullHandling.IgnoreSourceNulls
        }).Map(source);

        await Assert.That(mapNulls.DefaultValue).IsNull();
        await Assert.That(mapNulls.IgnoredValue).IsEqualTo("existing ignored");
        await Assert.That(mapNulls.MappedValue).IsNull();
        await Assert.That(mapNulls.AllowedValue).IsNull();
        await Assert.That(mapNulls.FallbackValue).IsEqualTo("fallback");

        await Assert.That(ignoreNulls.DefaultValue).IsEqualTo("existing default");
        await Assert.That(ignoreNulls.IgnoredValue).IsEqualTo("existing ignored");
        await Assert.That(ignoreNulls.MappedValue).IsNull();
        await Assert.That(ignoreNulls.AllowedValue).IsNull();
        await Assert.That(ignoreNulls.FallbackValue).IsEqualTo("fallback");
    }

    [Test]
    public async Task Flattening_And_Enums_Sample_Covers_Both_Path_Directions_And_Enum_Conversion()
    {
        var flattened = new FlatteningOfficeMapper().Map(
            new FlatteningOffice(
                new FlatteningOfficeLocation { City = "Riga" },
                FlatteningOfficeStatus.Open));
        var unflattened = new FlatteningOfficeImportMapper().Map(
            new FlatteningOfficeImport { LocationCity = "Tallinn" });

        await Assert.That(flattened.LocationCity).IsEqualTo("Riga");
        await Assert.That(flattened.Status).IsEqualTo(FlatteningOfficeStatusDto.Open);
        await Assert.That(unflattened.Location).IsNotNull();
        await Assert.That(unflattened.Location!.City).IsEqualTo("Tallinn");
    }

    [Test]
    public async Task Composition_Sample_Maps_Nested_Object_And_Ordered_Collection()
    {
        var mapper = new CompositionCustomerMapper(new CompositionAddressMapper());
        var target = mapper.Map(new CompositionCustomer
        {
            PrimaryAddress = new CompositionAddress("1 Compiler Way"),
            Addresses =
            [
                new CompositionAddress("2 Runtime Road"),
                new CompositionAddress("3 Generator Lane")
            ]
        });
        var empty = mapper.Map(new CompositionCustomer
        {
            PrimaryAddress = new CompositionAddress("Only address")
        });

        await Assert.That(target.PrimaryAddress.Line1).IsEqualTo("1 Compiler Way");
        await Assert.That(target.Addresses).Count().IsEqualTo(2);
        await Assert.That(target.Addresses[0].Line1).IsEqualTo("2 Runtime Road");
        await Assert.That(target.Addresses[1].Line1).IsEqualTo("3 Generator Lane");
        await Assert.That(empty.Addresses).IsEmpty();
    }

    [Test]
    public async Task Dependency_Injection_Sample_Covers_Typed_Facade_Factory_And_Try_APIs()
    {
        var services = new ServiceCollection();
        AddSampleMappers(services, options =>
        {
            options.Mode = ValiantMappingMode.Fast;
            options.NullHandling = ValiantNullHandling.MapNulls;
        });
        using var provider = services.BuildServiceProvider();
        var source = new DependencyPerson("Grace", 40);
        var typed = provider.GetRequiredService<IMapper<DependencyPerson, DependencyPersonDto>>();
        var facade = provider.GetRequiredService<IMapper>();
        var factory = provider.GetRequiredService<IMapperFactory>();

        var typedTarget = typed.Map(source);
        var facadeTarget = facade.Map<DependencyPersonDto>(source);
        var facadeFound = facade.TryMap<DependencyPersonDto>(source, out var tryTarget);
        var facadeMissing = facade.TryMap<string>(source, out _);
        var factoryMapper = factory.CreateMapper<DependencyPerson, DependencyPersonDto>();
        var factoryFound = factory.TryCreateMapper<DependencyPerson, DependencyPersonDto>(out var tryMapper);
        var factoryMissing = factory.TryCreateMapper<DependencyPerson, string>(out _);

        await Assert.That(provider.GetRequiredService<DependencyPersonMapper>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<ValiantMappingOptions>().Mode)
            .IsEqualTo(ValiantMappingMode.Fast);
        await Assert.That(typedTarget).IsEqualTo(new DependencyPersonDto("Grace", 40));
        await Assert.That(facadeTarget).IsEqualTo(new DependencyPersonDto("Grace", 40));
        await Assert.That(facadeFound).IsTrue();
        await Assert.That(tryTarget).IsEqualTo(new DependencyPersonDto("Grace", 40));
        await Assert.That(facadeMissing).IsFalse();
        await Assert.That(factoryMapper.Map(source)).IsEqualTo(new DependencyPersonDto("Grace", 40));
        await Assert.That(factoryFound).IsTrue();
        await Assert.That(tryMapper!.Map(source)).IsEqualTo(new DependencyPersonDto("Grace", 40));
        await Assert.That(factoryMissing).IsFalse();
    }

    private static void AddSampleMappers(
        IServiceCollection services,
        Action<ValiantMappingOptions> configure)
    {
        var extensionType = typeof(DependencyPersonMapper).Assembly.GetType(
            "StaticNorth.Valiant.Mapping.ValiantMappingServiceCollectionExtensions",
            throwOnError: true)!;
        var method = extensionType.GetMethod(
            "AddValiantMappers",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            [typeof(IServiceCollection), typeof(Action<ValiantMappingOptions>)],
            modifiers: null)!;

        _ = method.Invoke(null, [services, configure]);
    }
}
