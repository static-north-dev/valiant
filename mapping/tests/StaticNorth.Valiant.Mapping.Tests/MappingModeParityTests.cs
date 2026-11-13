using Microsoft.Extensions.DependencyInjection;
using StaticNorth.Valiant.Mapping.Tests.EdgeParity;
using CompositionAddress = StaticNorth.Valiant.Mapping.Samples.Composition.Address;
using CompositionAddressMapper = StaticNorth.Valiant.Mapping.Samples.Composition.AddressMapper;
using CompositionCustomer = StaticNorth.Valiant.Mapping.Samples.Composition.Customer;
using CompositionCustomerDto = StaticNorth.Valiant.Mapping.Samples.Composition.CustomerDto;
using CompositionCustomerMapper = StaticNorth.Valiant.Mapping.Samples.Composition.CustomerMapper;
using ConditionalContact = StaticNorth.Valiant.Mapping.Samples.ConditionalMapping.Contact;
using ConditionalContactDto = StaticNorth.Valiant.Mapping.Samples.ConditionalMapping.ContactDto;
using ConditionalContactMapper = StaticNorth.Valiant.Mapping.Samples.ConditionalMapping.ContactMapper;
using ExplicitUserProfile = StaticNorth.Valiant.Mapping.Samples.ExplicitMapping.UserProfile;
using ExplicitUserProfileDto = StaticNorth.Valiant.Mapping.Samples.ExplicitMapping.UserProfileDto;
using ExplicitUserProfileMapper = StaticNorth.Valiant.Mapping.Samples.ExplicitMapping.UserProfileMapper;
using NullProfile = StaticNorth.Valiant.Mapping.Samples.NullHandling.Profile;
using NullProfileDto = StaticNorth.Valiant.Mapping.Samples.NullHandling.ProfileDto;
using NullProfileMapper = StaticNorth.Valiant.Mapping.Samples.NullHandling.ProfileMapper;

namespace StaticNorth.Valiant.Mapping.Tests;

public sealed class MappingModeParityTests
{
    [Test]
    [Arguments(ValiantMappingMode.Fast)]
    [Arguments(ValiantMappingMode.Debuggable)]
    public async Task Conditional_Mapper_Uses_Only_The_Active_Branch(ValiantMappingMode mode)
    {
        var mapper = new EdgeConditionalNestedMapper(Options(mode));
        var value = new EdgeInner("Ada");

        var first = mapper.Map(new EdgeNestedInput(true, value) { Groups = [[value]] });
        var second = mapper.Map(new EdgeNestedInput(false, value) { Groups = [[value]] });

        await Assert.That(first.Value!.Name).IsEqualTo("first:Ada");
        await Assert.That(second.Value!.Name).IsEqualTo("second:Ada");
        await Assert.That(first.Groups.Single().Single().Name).IsEqualTo("first:Ada");
        await Assert.That(second.Groups.Single().Single().Name).IsEqualTo("second:Ada");
    }

    [Test]
    [Arguments(ValiantMappingMode.Fast)]
    [Arguments(ValiantMappingMode.Debuggable)]
    public async Task Conditional_Ignore_And_Source_Nulls_Preserve_Init_Default(ValiantMappingMode mode)
    {
        var mapper = new EdgeConditionalInitMapper(new ValiantMappingOptions
        {
            Mode = mode,
            NullHandling = ValiantNullHandling.IgnoreSourceNulls
        });

        await Assert.That(mapper.Map(new EdgeInitInput(false, null)).Value).IsEqualTo("original");
        await Assert.That(mapper.Map(new EdgeInitInput(true, "value")).Value).IsEqualTo("original");
        await Assert.That(mapper.Map(new EdgeInitInput(false, "value")).Value).IsEqualTo("value");
    }

    [Test]
    [Arguments(ValiantMappingMode.Fast)]
    [Arguments(ValiantMappingMode.Debuggable)]
    public async Task Native_Integer_Widening_Works_For_Constructor_Members(ValiantMappingMode mode)
    {
        var mapper = new EdgeNativeMapper(Options(mode));

        var converted = mapper.Map(new EdgeNativeInput(-7, uint.MaxValue, -10));
        var nullInput = mapper.Map(new EdgeNativeInput(0, null, 0));

        await Assert.That(converted.Value).IsEqualTo((nint)(-7));
        await Assert.That(converted.Count).IsEqualTo((nuint?)uint.MaxValue);
        await Assert.That(converted.Widen).IsEqualTo(-10L);
        await Assert.That(nullInput.Count).IsNull();
    }

    [Test]
    [Arguments(ValiantMappingMode.Fast)]
    [Arguments(ValiantMappingMode.Debuggable)]
    public async Task Nested_Mapper_Uses_The_Requested_Explicit_Interface(ValiantMappingMode mode)
    {
        var mapper = new EdgeDerivedNestedMapper(Options(mode));

        await Assert.That(mapper.Map(new EdgeNestedInput(true, new EdgeInner("Ada"))).Value!.Name).IsEqualTo("Ada");
    }

    [Test]
    public async Task Explicit_Transform_Rename_And_Ignore_Have_Mode_Parity()
    {
        await AssertParity(
            mode => new ExplicitUserProfileMapper(Options(mode)),
            static () => new ExplicitUserProfile(
                "Ada",
                "Lovelace",
                "ada@example.test",
                "private"),
            static target => $"{target.DisplayName}|{target.Email}|{target.InternalNote}",
            "Ada Lovelace|ada@example.test|not exposed");
    }

    [Test]
    public async Task Conditional_Chains_Fallbacks_And_Native_Branches_Have_Mode_Parity()
    {
        await AssertParity(
            mode => new ConditionalContactMapper(Options(mode)),
            static () => new ConditionalContact
            {
                Name = "Ada",
                Email = "ada@example.test",
                Phone = "+371 555 0100"
            },
            ContactSnapshot,
            "Ada|Private email|Private phone");
        await AssertParity(
            mode => new ConditionalContactMapper(Options(mode)),
            static () => new ConditionalContact
            {
                UseEmailAsDisplayName = true,
                IncludeContactDetails = true,
                Name = "Ada",
                Email = "ada@example.test",
                Phone = "+371 555 0100"
            },
            ContactSnapshot,
            "ada@example.test|ada@example.test|+371 555 0100");
    }

    [Test]
    public async Task Null_Option_And_Per_Rule_Overrides_Have_Mode_Parity()
    {
        await AssertParity(
            mode => new NullProfileMapper(Options(mode, ValiantNullHandling.MapNulls)),
            static () => new NullProfile(),
            ProfileSnapshot,
            "<null>|existing ignored|<null>|<null>|fallback");
        await AssertParity(
            mode => new NullProfileMapper(Options(mode, ValiantNullHandling.IgnoreSourceNulls)),
            static () => new NullProfile(),
            ProfileSnapshot,
            "existing default|existing ignored|<null>|<null>|fallback");
        await AssertParity(
            mode => new NullProfileMapper(Options(mode, ValiantNullHandling.IgnoreSourceNulls)),
            static () => new NullProfile
            {
                DefaultValue = "default",
                IgnoredValue = "ignored",
                MappedValue = "mapped",
                AllowedValue = "allowed",
                FallbackValue = "provided"
            },
            ProfileSnapshot,
            "default|ignored|mapped|allowed|provided");
    }

    [Test]
    public async Task Nested_And_Collection_Composition_Have_Mode_Parity()
    {
        await AssertParity(
            mode => new CompositionCustomerMapper(new CompositionAddressMapper(), Options(mode)),
            static () => new CompositionCustomer
            {
                PrimaryAddress = new CompositionAddress("1 Compiler Way"),
                Addresses =
                [
                    new CompositionAddress("2 Runtime Road"),
                    new CompositionAddress("3 Generator Lane")
                ]
            },
            CustomerSnapshot,
            "1 Compiler Way|2 Runtime Road,3 Generator Lane");
        await AssertParity(
            mode => new CompositionCustomerMapper(new CompositionAddressMapper(), Options(mode)),
            static () => new CompositionCustomer
            {
                PrimaryAddress = new CompositionAddress("Only address")
            },
            CustomerSnapshot,
            "Only address|");
    }

    [Test]
    public async Task Null_Root_Source_Throws_Equivalent_Exception_In_Both_Modes()
    {
        var fast = await CaptureExceptionAsync(() =>
            Task.FromResult(new ExplicitUserProfileMapper(Options(ValiantMappingMode.Fast)).Map(null!)));
        var debuggable = await CaptureExceptionAsync(() =>
            Task.FromResult(new ExplicitUserProfileMapper(Options(ValiantMappingMode.Debuggable)).Map(null!)));

        await Assert.That(ExceptionSnapshot(fast))
            .IsEqualTo("System.ArgumentNullException|source");
        await Assert.That(ExceptionSnapshot(debuggable))
            .IsEqualTo("System.ArgumentNullException|source");
    }

    [Test]
    public async Task Struct_Assignments_And_Set_Conversion_Have_Mode_Parity()
    {
        await AssertParity(
            mode => new StructCollectionParityMapper(Options(mode)),
            static () => new StructCollectionParitySource(42, [3, 1, 3]),
            static target => $"{target.Value}|{string.Join(",", target.Items.Order())}",
            "42|1,3");
    }

    [Test]
    [Arguments(true, true, "preferred|7|2,4|constructor")]
    [Arguments(false, true, "alternate|7|2,4|constructor")]
    [Arguments(false, false, "unknown|7|2,4|constructor")]
    public async Task Constructor_Selection_Conditional_Arguments_And_Init_Members_Have_Mode_Parity(
        bool preferred,
        bool alternate,
        string expected)
    {
        await AssertParity(
            mode => new ImmutableParityMapper(Options(mode)),
            () => new ImmutableParitySource(preferred, alternate),
            static target => $"{target.Name}|{target.Number}|{string.Join(",", target.Numbers!)}|{target.Constructor}",
            expected);
    }

    [Test]
    [Arguments(ValiantMappingMode.Fast)]
    [Arguments(ValiantMappingMode.Debuggable)]
    public async Task Null_Converted_Collection_And_Constructor_Exception_Have_Mode_Parity(ValiantMappingMode mode)
    {
        var mapper = new ImmutableParityMapper(Options(mode));
        var target = mapper.Map(new ImmutableParitySource(true, false) { Numbers = null });
        await Assert.That(target.Numbers).IsNull();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.FromResult(
            mapper.Map(new ImmutableParitySource(true, false) { NameOverride = "invalid" })));
        await Assert.That(exception!.Message).IsEqualTo("Cannot construct target.");
    }

    [Test]
    [Arguments(ValiantMappingMode.Fast, false, false)]
    [Arguments(ValiantMappingMode.Debuggable, false, false)]
    [Arguments(ValiantMappingMode.Fast, true, false)]
    [Arguments(ValiantMappingMode.Debuggable, true, false)]
    [Arguments(ValiantMappingMode.Fast, false, true)]
    [Arguments(ValiantMappingMode.Debuggable, false, true)]
    public async Task Ignore_Source_Nulls_Reads_Each_Active_Selector_Once(
        ValiantMappingMode mode,
        bool firstReadIsNull,
        bool skipValue)
    {
        var source = new SingleReadSource(firstReadIsNull, skipValue);
        var target = new SingleReadMapper(Options(mode, ValiantNullHandling.IgnoreSourceNulls)).Map(source);

        await Assert.That(target.ConstructorValue).IsEqualTo(firstReadIsNull ? "fallback" : "first");
        await Assert.That(target.InitValue).IsEqualTo(firstReadIsNull ? null : "first");
        await Assert.That(target.Value).IsEqualTo(firstReadIsNull || skipValue ? "original" : "first");
        await Assert.That((source.ConstructorReads, source.InitReads, source.ValueReads)).IsEqualTo((1, 1, skipValue ? 0 : 1));
    }

    [Test]
    [Arguments(ValiantMappingMode.Fast)]
    [Arguments(ValiantMappingMode.Debuggable)]
    public async Task Unused_Fallback_Is_Evaluated_Once_Before_Mapping(ValiantMappingMode mode)
    {
        var mapper = new AssignmentParityMapper(Options(mode));
        var unused = new AssignmentParitySource();
        var used = new AssignmentParitySource(value: null);
        await Assert.That(mapper.Map(unused).Second).IsEqualTo("mapped");
        await Assert.That(mapper.Map(used).Second).IsEqualTo("fallback");
        await Assert.That(unused.Calls.First()).IsEqualTo("fallback");
        await Assert.That(used.Calls.First()).IsEqualTo("fallback");
        await Assert.That(unused.Calls.Count(call => call == "fallback")).IsEqualTo(1);
        await Assert.That(used.Calls.Count(call => call == "fallback")).IsEqualTo(1);
        await Assert.That(unused.Calls.Count(call => call == "branch")).IsEqualTo(1);
        await Assert.That(unused.Calls).Contains("primary");
        await Assert.That(unused.Calls).DoesNotContain("secondary");
    }

    [Test]
    [Arguments(ValiantMappingMode.Fast)]
    [Arguments(ValiantMappingMode.Debuggable)]
    public async Task Setters_Run_In_Rule_Declaration_Order(ValiantMappingMode mode)
    {
        var source = new AssignmentParitySource();
        var target = new AssignmentParityMapper(Options(mode)).Map(source);

        await Assert.That(string.Join(",", source.Calls)).IsEqualTo("fallback,branch,primary,second,init,first");
        await Assert.That((target.First, target.Second, target.Initial, target.InitWrites))
            .IsEqualTo(("mapped", "mapped", "mapped", 1));
    }

    [Test]
    [Arguments(ValiantMappingMode.Fast)]
    [Arguments(ValiantMappingMode.Debuggable)]
    public async Task Conditionally_Ignored_Init_Only_Property_Keeps_Initializer(ValiantMappingMode mode)
    {
        var source = new AssignmentParitySource(skipInit: true);
        var target = new AssignmentParityMapper(Options(mode)).Map(source);

        await Assert.That((target.Initial, target.InitWrites)).IsEqualTo(("original", 0));
        await Assert.That(source.Calls).DoesNotContain("init");
    }

    [Test]
    [Arguments(ValiantMappingMode.Auto, 1, 0)]
    [Arguments(ValiantMappingMode.Fast, 1, 0)]
    [Arguments(ValiantMappingMode.Debuggable, 0, 1)]
    public async Task Explicit_Mode_Dispatch_Uses_The_Selected_Backend(
        ValiantMappingMode mode, int fastCaptures, int debuggableCaptures)
    {
        var probe = new MappingModeProbe();
        var target = new MappingModeProbeMapper(probe, Options(mode)).Map(new MappingModeSource { Value = "mapped" });

        await Assert.That(target.Value).IsEqualTo("mapped");
        await Assert.That(probe.FastCaptures).IsEqualTo(fastCaptures);
        await Assert.That(probe.DebuggableCaptures).IsEqualTo(debuggableCaptures);
    }

    [Test]
    public async Task Mapper_Without_Options_Uses_The_Generated_Backend()
    {
        var probe = new MappingModeProbe();
        var target = new MappingModeProbeMapper(probe).Map(new MappingModeSource { Value = "mapped" });

        await Assert.That(target.Value).IsEqualTo("mapped");
        await Assert.That(probe.FastCaptures).IsEqualTo(1);
        await Assert.That(probe.DebuggableCaptures).IsEqualTo(0);
    }

    [Test]
    [Arguments(ValiantMappingMode.Auto, false, 1, 0)]
    [Arguments(ValiantMappingMode.Auto, true, 1, 0)]
    [Arguments(ValiantMappingMode.Fast, false, 1, 0)]
    [Arguments(ValiantMappingMode.Fast, true, 1, 0)]
    [Arguments(ValiantMappingMode.Debuggable, false, 0, 1)]
    [Arguments(ValiantMappingMode.Debuggable, true, 0, 1)]
    public async Task DI_And_Factory_Select_The_Same_Backend_For_Parent_And_Nested_Mappers(
        ValiantMappingMode mode, bool useFactory, int fastCaptures, int debuggableCaptures)
    {
        var parentProbe = new MappingModeProbe();
        var nestedProbe = new MappingModeProbe();
        var services = new ServiceCollection();
        services.AddSingleton(nestedProbe);
        // The sample assembly also exposes a generated registration class with this name.
#pragma warning disable CS0436
        ValiantMappingServiceCollectionExtensions.AddValiantMappers(services, options => options.Mode = mode);
#pragma warning restore CS0436
        using var provider = services.BuildServiceProvider();

        var mapper = useFactory
            ? provider.GetRequiredService<IMapperFactory>().CreateMapper<MappingModeParentSource, MappingModeParentTarget>()
            : provider.GetRequiredService<IMapper<MappingModeParentSource, MappingModeParentTarget>>();
        var source = new MappingModeParentSource(parentProbe, new MappingModeSource { Value = "nested value" });
        var target = mapper.Map(source);

        await Assert.That(target).IsEqualTo(new MappingModeParentTarget(new MappingModeTarget("nested value")));
        await Assert.That(parentProbe.FastCaptures).IsEqualTo(fastCaptures);
        await Assert.That(parentProbe.DebuggableCaptures).IsEqualTo(debuggableCaptures);
        await Assert.That(nestedProbe.FastCaptures).IsEqualTo(fastCaptures);
        await Assert.That(nestedProbe.DebuggableCaptures).IsEqualTo(debuggableCaptures);
    }

    private static async Task AssertParity<TSource, TTarget>(
        Func<ValiantMappingMode, IMapper<TSource, TTarget>> createMapper,
        Func<TSource> createSource,
        Func<TTarget, string> snapshot,
        string expected)
    {
        var fastTarget = createMapper(ValiantMappingMode.Fast).Map(createSource());
        var debuggableTarget = createMapper(ValiantMappingMode.Debuggable).Map(createSource());
        var fastSnapshot = snapshot(fastTarget);
        var debuggableSnapshot = snapshot(debuggableTarget);

        await Assert.That(fastSnapshot).IsEqualTo(expected);
        await Assert.That(debuggableSnapshot).IsEqualTo(expected);
        await Assert.That(debuggableSnapshot).IsEqualTo(fastSnapshot);
    }

    private static string ContactSnapshot(ConditionalContactDto target)
    {
        return $"{target.DisplayName}|{target.Email}|{target.Phone}";
    }

    private static string ProfileSnapshot(NullProfileDto target)
    {
        return string.Join(
            "|",
            target.DefaultValue ?? "<null>",
            target.IgnoredValue ?? "<null>",
            target.MappedValue ?? "<null>",
            target.AllowedValue ?? "<null>",
            target.FallbackValue ?? "<null>");
    }

    private static string CustomerSnapshot(CompositionCustomerDto target)
    {
        return $"{target.PrimaryAddress.Line1}|{string.Join(",", target.Addresses.Select(static address => address.Line1))}";
    }

    private static ValiantMappingOptions Options(
        ValiantMappingMode mode,
        ValiantNullHandling nullHandling = ValiantNullHandling.MapNulls)
    {
        return new ValiantMappingOptions
        {
            Mode = mode,
            NullHandling = nullHandling
        };
    }

    private static async Task<Exception> CaptureExceptionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected the action to throw.");
    }

    private static string ExceptionSnapshot(Exception exception)
    {
        return exception is ArgumentException argumentException
            ? $"{exception.GetType().FullName}|{argumentException.ParamName}"
            : exception.GetType().FullName ?? exception.GetType().Name;
    }
}

public sealed class MappingModeSource
{
    public string? Value { get; init; }
}

public sealed record MappingModeTarget(string? Value);

public sealed class MappingModeProbe
{
    public int FastCaptures { get; private set; }

    public int DebuggableCaptures { get; private set; }

    public bool CaptureBackend([System.Runtime.CompilerServices.CallerMemberName] string? caller = null)
    {
        if (string.Equals(caller, "Map", StringComparison.Ordinal))
        {
            FastCaptures++;
        }
        else if (string.Equals(caller, "Configure", StringComparison.Ordinal))
        {
            DebuggableCaptures++;
        }

        return true;
    }
}

[ValiantMapper]
public sealed partial class MappingModeProbeMapper(
    MappingModeProbe probe) : MapperDefinition<MappingModeSource, MappingModeTarget>
{
    public override void Configure(MappingContext<MappingModeSource, MappingModeTarget> context)
    {
        if (probe.CaptureBackend())
        {
            context.Map(source => source.Value, target => target.Value);
        }
    }
}
