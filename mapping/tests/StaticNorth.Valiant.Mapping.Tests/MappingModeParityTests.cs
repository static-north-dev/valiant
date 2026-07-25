using CompositionAddress = Valiant.Mapping.Samples.Composition.Address;
using CompositionAddressMapper = Valiant.Mapping.Samples.Composition.AddressMapper;
using CompositionCustomer = Valiant.Mapping.Samples.Composition.Customer;
using CompositionCustomerDto = Valiant.Mapping.Samples.Composition.CustomerDto;
using CompositionCustomerMapper = Valiant.Mapping.Samples.Composition.CustomerMapper;
using ConditionalContact = Valiant.Mapping.Samples.ConditionalMapping.Contact;
using ConditionalContactDto = Valiant.Mapping.Samples.ConditionalMapping.ContactDto;
using ConditionalContactMapper = Valiant.Mapping.Samples.ConditionalMapping.ContactMapper;
using ExplicitUserProfile = Valiant.Mapping.Samples.ExplicitMapping.UserProfile;
using ExplicitUserProfileDto = Valiant.Mapping.Samples.ExplicitMapping.UserProfileDto;
using ExplicitUserProfileMapper = Valiant.Mapping.Samples.ExplicitMapping.UserProfileMapper;
using NullProfile = Valiant.Mapping.Samples.NullHandling.Profile;
using NullProfileDto = Valiant.Mapping.Samples.NullHandling.ProfileDto;
using NullProfileMapper = Valiant.Mapping.Samples.NullHandling.ProfileMapper;

namespace Valiant.Mapping.Tests;

public sealed class MappingModeParityTests
{
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
    public async Task Explicit_And_Auto_Mode_Dispatch_Execute_The_Selected_Backend()
    {
        var fastProbe = new MappingModeProbe();
        var fastTarget = new MappingModeProbeMapper(
            fastProbe,
            Options(ValiantMappingMode.Fast)).Map(new MappingModeSource { Value = "fast" });
        await Assert.That(fastTarget.Value).IsEqualTo("fast");
        await Assert.That(fastProbe.FastCaptures).IsEqualTo(1);
        await Assert.That(fastProbe.DebuggableCaptures).IsEqualTo(0);

        var debuggableProbe = new MappingModeProbe();
        var debuggableTarget = new MappingModeProbeMapper(
            debuggableProbe,
            Options(ValiantMappingMode.Debuggable)).Map(new MappingModeSource { Value = "debuggable" });
        await Assert.That(debuggableTarget.Value).IsEqualTo("debuggable");
        await Assert.That(debuggableProbe.FastCaptures).IsEqualTo(0);
        await Assert.That(debuggableProbe.DebuggableCaptures).IsEqualTo(1);

        var autoProbe = new MappingModeProbe();
        var autoTarget = new MappingModeProbeMapper(
            autoProbe,
            Options(ValiantMappingMode.Auto)).Map(new MappingModeSource { Value = "auto" });
        await Assert.That(autoTarget.Value).IsEqualTo("auto");
#if DEBUG
        await Assert.That(autoProbe.FastCaptures).IsEqualTo(0);
        await Assert.That(autoProbe.DebuggableCaptures).IsEqualTo(1);
#else
        await Assert.That(autoProbe.FastCaptures).IsEqualTo(1);
        await Assert.That(autoProbe.DebuggableCaptures).IsEqualTo(0);
#endif
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

public sealed class MappingModeTarget
{
    public string? Value { get; set; }
}

public sealed class MappingModeProbe
{
    public int FastCaptures { get; private set; }

    public int DebuggableCaptures { get; private set; }

    public string? Capture(
        string? value,
        [System.Runtime.CompilerServices.CallerMemberName] string? caller = null)
    {
        if (string.Equals(caller, "Map", StringComparison.Ordinal))
        {
            FastCaptures++;
        }
        else if (string.Equals(caller, "Configure", StringComparison.Ordinal))
        {
            DebuggableCaptures++;
        }

        return value;
    }
}

[ValiantMapper]
public sealed partial class MappingModeProbeMapper(
    MappingModeProbe probe) : MapperDefinition<MappingModeSource, MappingModeTarget>
{
    public override void Configure(MappingContext<MappingModeSource, MappingModeTarget> context)
    {
        context.Map(source => probe.Capture(source.Value), target => target.Value);
    }
}
