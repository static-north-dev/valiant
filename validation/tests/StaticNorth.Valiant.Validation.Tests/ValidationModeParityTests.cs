using System.Globalization;
using System.Reflection;
using SampleAddressPropertyValidator = StaticNorth.Valiant.Validation.Samples.Catalog.AddressPropertyValidator;
using SampleAddressValidator = StaticNorth.Valiant.Validation.Samples.Catalog.AddressValidator;
using SampleCatalogItemFactory = StaticNorth.Valiant.Validation.Samples.Catalog.CatalogItemFactory;
using SampleCatalogItemRulesValidator = StaticNorth.Valiant.Validation.Samples.Catalog.CatalogItemRulesValidator;
using SampleCatalogItemValidator = StaticNorth.Valiant.Validation.Samples.Catalog.CatalogItemValidator;

namespace StaticNorth.Valiant.Validation.Tests;

/// <summary>
/// Verifies that the generated direct backend and the runtime Configure backend
/// produce the same observable behavior for every supported validation family.
/// Existing rule-specific tests remain the independent semantic oracle.
/// </summary>
public sealed class ValidationModeParityTests
{
    [Test]
    public async Task Null_String_And_Length_Rules_Have_Mode_Parity()
    {
        await AssertSyncParity(
            "NotNull",
            mode => new NotNullValidator(Options(mode)),
            Invalid(static () => new NullableTextModel(), Failure("Value", "NotNull")),
            Valid(static () => new NullableTextModel { Value = "value" }));
        await AssertSyncParity(
            "Null",
            mode => new NullValidator(Options(mode)),
            Valid(static () => new NullableTextModel()),
            Invalid(static () => new NullableTextModel { Value = "value" }, Failure("Value", "Null")));
        await AssertSyncParity(
            "Required",
            mode => new RequiredValidator(Options(mode)),
            Invalid(static () => new NullableTextModel(), Failure("Value", "Required")),
            Invalid(static () => new NullableTextModel { Value = " " }, Failure("Value", "Required")),
            Valid(static () => new NullableTextModel { Value = "value" }));
        await AssertSyncParity(
            "NotEmpty",
            mode => new NotEmptyValidator(Options(mode)),
            Invalid(static () => new NullableTextModel { Value = "" }, Failure("Value", "NotEmpty")),
            Valid(static () => new NullableTextModel { Value = " " }));
        await AssertSyncParity(
            "Empty",
            mode => new EmptyValidator(Options(mode)),
            Valid(static () => new NullableTextModel()),
            Invalid(static () => new NullableTextModel { Value = "value" }, Failure("Value", "Empty")));
        await AssertSyncParity(
            "LengthBetween",
            mode => new LengthBetweenValidator(Options(mode)),
            Invalid(static () => new NullableTextModel { Value = "a" }, Failure("Value", "LengthBetween")),
            Valid(static () => new NullableTextModel { Value = "ab" }),
            Invalid(static () => new NullableTextModel { Value = "abcd" }, Failure("Value", "LengthBetween")));
        await AssertSyncParity(
            "MinLength",
            mode => new MinLengthValidator(Options(mode)),
            Invalid(static () => new NullableTextModel { Value = "a" }, Failure("Value", "MinLength")),
            Valid(static () => new NullableTextModel { Value = "ab" }));
        await AssertSyncParity(
            "MaxLength",
            mode => new MaxLengthValidator(Options(mode)),
            Valid(static () => new NullableTextModel { Value = "abc" }),
            Invalid(static () => new NullableTextModel { Value = "abcd" }, Failure("Value", "MaxLength")));
        await AssertSyncParity(
            "NonNullableMaxLength",
            mode => new NonNullableStringMaxLengthValidator(Options(mode)),
            Valid(static () => new NonNullableTextModel("abc")),
            Invalid(static () => new NonNullableTextModel("abcd"), Failure("Value", "MaxLength")));
    }

    [Test]
    public async Task Comparison_Boundary_And_Custom_Rules_Have_Mode_Parity()
    {
        await AssertSyncParity(
            "GreaterThan",
            mode => new GreaterThanValidator(Options(mode)),
            Invalid(static () => new NumberModel { Value = 10 }, Failure("Value", "GreaterThan")),
            Valid(static () => new NumberModel { Value = 11 }));
        await AssertSyncParity(
            "NullableDecimalGreaterThan",
            mode => new NullableDecimalGreaterThanValidator(Options(mode)),
            Valid(static () => new NullableDecimalModel()),
            Invalid(static () => new NullableDecimalModel { Value = 0 }, Failure("Value", "GreaterThan")));
        await AssertSyncParity(
            "NullableByteAndSByteGreaterThan",
            mode => new NullableByteGreaterThanValidator(Options(mode)),
            Valid(static () => new NullableByteModel()),
            Invalid(
                static () => new NullableByteModel { Byte = 0, SByte = 0 },
                Failure("Byte", "GreaterThan"),
                Failure("SByte", "GreaterThan")));
        await AssertSyncParity(
            "GreaterThanOrEqualTo",
            mode => new GreaterThanOrEqualToValidator(Options(mode)),
            Invalid(static () => new NumberModel { Value = 9 }, Failure("Value", "GreaterThanOrEqualTo")),
            Valid(static () => new NumberModel { Value = 10 }));
        await AssertSyncParity(
            "LessThan",
            mode => new LessThanValidator(Options(mode)),
            Invalid(static () => new NumberModel { Value = 10 }, Failure("Value", "LessThan")),
            Valid(static () => new NumberModel { Value = 9 }));
        await AssertSyncParity(
            "LessThanOrEqualTo",
            mode => new LessThanOrEqualToValidator(Options(mode)),
            Invalid(static () => new NumberModel { Value = 11 }, Failure("Value", "LessThanOrEqualTo")),
            Valid(static () => new NumberModel { Value = 10 }));
        await AssertSyncParity(
            "Equal",
            mode => new EqualValidator(Options(mode)),
            Invalid(static () => new NumberModel { Value = 11 }, Failure("Value", "Equal")),
            Valid(static () => new NumberModel { Value = 10 }));
        await AssertSyncParity(
            "NotEqual",
            mode => new NotEqualValidator(Options(mode)),
            Invalid(static () => new NumberModel { Value = 10 }, Failure("Value", "NotEqual")),
            Valid(static () => new NumberModel { Value = 11 }));
        await AssertSyncParity(
            "InclusiveBetween",
            mode => new InclusiveBetweenValidator(Options(mode)),
            Invalid(static () => new NumberModel { Value = 4 }, Failure("Value", "InclusiveBetween")),
            Valid(static () => new NumberModel { Value = 5 }),
            Valid(static () => new NumberModel { Value = 10 }));
        await AssertSyncParity(
            "ExclusiveBetween",
            mode => new ExclusiveBetweenValidator(Options(mode)),
            Invalid(static () => new NumberModel { Value = 5 }, Failure("Value", "ExclusiveBetween")),
            Valid(static () => new NumberModel { Value = 6 }),
            Invalid(static () => new NumberModel { Value = 10 }, Failure("Value", "ExclusiveBetween")));
        await AssertSyncParity(
            "Satisfies",
            mode => new SatisfiesValidator(Options(mode)),
            Invalid(static () => new NullableTextModel { Value = "!" }, Failure("Value", "Satisfies")),
            Valid(static () => new NullableTextModel { Value = "ok" }));
        await AssertSyncParity(
            "SatisfiesWithModel",
            mode => new SatisfiesWithModelValidator(Options(mode)),
            Invalid(static () => new CompareModel { Left = 1, Right = 2 }, Failure("Left", "Satisfies")),
            Valid(static () => new CompareModel { Left = 2, Right = 2 }));
    }

    [Test]
    public async Task Metadata_Severity_Enum_And_Culture_Sensitive_Messages_Have_Mode_Parity()
    {
        await AssertSyncParity(
            "Metadata",
            mode => new MetadataValidator(Options(mode)),
            Invalid(
                static () => new NullableTextModel(),
                new FailureExpectation("Value", "ValueRequired", ValiantSeverity.Error, "Custom message.")));
        await AssertSyncParity(
            "WarningSeverity",
            mode => new WarningSeverityValidator(Options(mode)),
            ValidWithFailures(
                static () => new NullableTextModel(),
                Failure("Value", "Required", ValiantSeverity.Warning)));
        await AssertSyncParity(
            "DefinedEnum",
            mode => new DefinedEnumValidator(Options(mode)),
            Invalid(
                static () => new EnumValidationModel { State = (SampleState)99 },
                Failure("State", "DefinedEnum")),
            Valid(static () => new EnumValidationModel { State = SampleState.Started }));
        await AssertSyncParity(
            "EnumName",
            mode => new EnumNameValidator(Options(mode)),
            Invalid(
                static () => new EnumValidationModel { StateName = "Missing" },
                Failure("StateName", "EnumName")),
            Valid(static () => new EnumValidationModel { StateName = nameof(SampleState.Started) }));
        await AssertSyncParity(
            "EnumValue",
            mode => new EnumValueValidator(Options(mode)),
            Invalid(
                static () => new EnumValidationModel { StateValue = 99 },
                Failure("StateValue", "EnumValue")),
            Valid(static () => new EnumValidationModel { StateValue = (int)SampleState.Started }));

        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            await AssertSyncParity(
                "CultureSensitiveDecimalMessage",
                mode => new CultureDecimalValidator(Options(mode)),
                Invalid(
                    static () => new CultureDecimalModel { Value = 1m },
                    Failure("Value", "GreaterThan")));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Test]
    public async Task Conditional_Switch_And_Native_Control_Flow_Have_Mode_Parity()
    {
        await AssertSyncParity(
            "IfElse",
            mode => new IfElseConditionalValidator(Options(mode)),
            Invalid(
                static () => new ConditionalModel { ValidateName = true, Name = "" },
                Failure("Name", "Required")),
            Invalid(
                static () => new ConditionalModel { ValidateName = false, Age = 17 },
                Failure("Age", "GreaterThanOrEqualTo")),
            Valid(static () => new ConditionalModel { ValidateName = true, Name = "ok", Age = 17 }));
        await AssertSyncParity(
            "Switch",
            mode => new SwitchConditionalValidator(Options(mode)),
            Invalid(
                static () => new ConditionalModel { Mode = ConditionalMode.Name, Name = "" },
                Failure("Name", "Required")),
            Invalid(
                static () => new ConditionalModel
                {
                    Mode = ConditionalMode.Age,
                    RequireAdult = true,
                    Age = 17
                },
                Failure("Age", "GreaterThanOrEqualTo")),
            Valid(static () => new ConditionalModel { Mode = ConditionalMode.None }),
            Valid(static () => new ConditionalModel
            {
                Mode = ConditionalMode.Age,
                RequireAdult = false,
                Age = 17
            }));
        await AssertSyncParity(
            "When",
            mode => new ConditionalWhenValidator(Options(mode)),
            Invalid(
                static () => new ConditionalModel { RequireAdult = true, Age = 17 },
                Failure("Age", "GreaterThanOrEqualTo")),
            Valid(static () => new ConditionalModel { RequireAdult = false, Age = 17 }));
        await AssertSyncParity(
            "NativeLoops",
            mode => new LoopValidator(Options(mode)),
            Valid(static () => new LoopModel()),
            Invalid(
                static () => new LoopModel
                {
                    Values = [-1, -2],
                    Name = "",
                    RunWhile = true,
                    RunDo = true
                },
                Failure("Name", "ForeachName"),
                Failure("Name", "ForeachName"),
                Failure("Name", "ForName"),
                Failure("Name", "ForName"),
                Failure("Name", "WhileName"),
                Failure("Name", "DoName")));
    }

    [Test]
    public async Task Collection_Dictionary_And_Indexed_Rules_Have_Mode_Parity()
    {
        await AssertSyncParity(
            "NotEmptyCollection",
            mode => new NotEmptyCollectionValidator(Options(mode)),
            Invalid(static () => new CollectionModel(), Failure("Values", "NotEmpty")),
            Valid(static () => new CollectionModel { Values = [1] }));
        await AssertSyncParity(
            "Count",
            mode => new CountCollectionValidator(Options(mode)),
            Invalid(static () => new CollectionModel { Values = [1, 2] }, Failure("Values", "Count")),
            Valid(static () => new CollectionModel { Values = [1, 2, 3] }));
        await AssertSyncParity(
            "CountBetween",
            mode => new CountBetweenCollectionValidator(Options(mode)),
            Invalid(
                static () => new CollectionModel { Values = [1, 2, 3, 4] },
                Failure("Values", "CountBetween")),
            Valid(static () => new CollectionModel { Values = [1, 2] }));
        await AssertSyncParity(
            "MinCount",
            mode => new MinCountCollectionValidator(Options(mode)),
            Invalid(static () => new CollectionModel { Values = [1] }, Failure("Values", "MinCount")),
            Valid(static () => new CollectionModel { Values = [1, 2] }));
        await AssertSyncParity(
            "MaxCount",
            mode => new MaxCountCollectionValidator(Options(mode)),
            Invalid(
                static () => new CollectionModel { Values = [1, 2, 3, 4] },
                Failure("Values", "MaxCount")),
            Valid(static () => new CollectionModel { Values = [1, 2, 3] }));
        await AssertSyncParity(
            "LengthBetweenCollection",
            mode => new LengthBetweenCollectionValidator(Options(mode)),
            Invalid(
                static () => new CollectionModel { Values = [1, 2, 3, 4] },
                Failure("Values", "LengthBetween")),
            Valid(static () => new CollectionModel { Values = [1, 2] }));
        await AssertSyncParity(
            "MinLengthCollection",
            mode => new MinLengthCollectionValidator(Options(mode)),
            Invalid(static () => new CollectionModel { Values = [1] }, Failure("Values", "MinLength")),
            Valid(static () => new CollectionModel { Values = [1, 2] }));
        await AssertSyncParity(
            "MaxLengthCollection",
            mode => new MaxLengthCollectionValidator(Options(mode)),
            Invalid(
                static () => new CollectionModel { Values = [1, 2, 3, 4] },
                Failure("Values", "MaxLength")),
            Valid(static () => new CollectionModel { Values = [1, 2, 3] }));
        await AssertSyncParity(
            "Contains",
            mode => new ContainsCollectionValidator(Options(mode)),
            Invalid(static () => new CollectionModel { Values = [1, 2] }, Failure("Values", "Contains")),
            Valid(static () => new CollectionModel { Values = [1, 2, 3] }));
        await AssertSyncParity(
            "Excludes",
            mode => new ExcludesCollectionValidator(Options(mode)),
            Invalid(
                static () => new CollectionModel { Values = [1, 99] },
                Failure("Values", "Excludes")),
            Valid(static () => new CollectionModel { Values = [1, 2] }));
        await AssertSyncParity(
            "All",
            mode => new AllCollectionValidator(Options(mode)),
            Invalid(static () => new CollectionModel { Values = [1, -1] }, Failure("Values", "All")),
            Valid(static () => new CollectionModel { Values = [1, 2] }));
        await AssertSyncParity(
            "Any",
            mode => new AnyCollectionValidator(Options(mode)),
            Invalid(static () => new CollectionModel { Values = [1, 2] }, Failure("Values", "Any")),
            Valid(static () => new CollectionModel { Values = [1, 42] }));
        await AssertSyncParity(
            "None",
            mode => new NoneCollectionValidator(Options(mode)),
            Invalid(static () => new CollectionModel { Values = [1, -1] }, Failure("Values", "None")),
            Valid(static () => new CollectionModel { Values = [1, 2] }));
        await AssertSyncParity(
            "HasKey",
            mode => new HasKeyDictionaryValidator(Options(mode)),
            Invalid(static () => new DictionaryModel(), Failure("Values", "HasKey")),
            Invalid(
                static () => new DictionaryModel
                {
                    Values = new Dictionary<string, int> { ["other"] = 7 }
                },
                Failure("Values", "HasKey")),
            Valid(static () => new DictionaryModel
            {
                Values = new Dictionary<string, int> { ["id"] = 1 }
            }));
        await AssertSyncParity(
            "HasValue",
            mode => new HasValueDictionaryValidator(Options(mode)),
            Invalid(static () => new DictionaryModel(), Failure("Values", "HasValue")),
            Invalid(
                static () => new DictionaryModel
                {
                    Values = new Dictionary<string, int> { ["id"] = 1 }
                },
                Failure("Values", "HasValue")),
            Valid(static () => new DictionaryModel
            {
                Values = new Dictionary<string, int> { ["id"] = 7 }
            }));
        await AssertSyncParity(
            "ForEach",
            mode => new ChildCollectionValidator(Options(mode)),
            Valid(static () => new ParentModel()),
            Invalid(
                static () => new ParentModel
                {
                    Children =
                    [
                        new ChildModel { Name = "" },
                        new ChildModel { Name = "ok" },
                        new ChildModel { Name = "" }
                    ]
                },
                Failure("Children[0].Name", "Required"),
                Failure("Children[2].Name", "Required")));
        await AssertSyncParity(
            "ForEachSeverityComposition",
            mode => new CollectionSeverityValidator(Options(mode)),
            ValidWithFailures(
                static () => new ParentModel
                {
                    Children = [new ChildModel { Name = "" }]
                },
                Failure("Children[0].Name", "ChildWarning", ValiantSeverity.Warning)));
        await AssertSyncParity(
            "ForEachWithCollection",
            mode => new ChildCollectionWithCollectionParameterValidator(Options(mode)),
            Invalid(
                static () => new ParentModel
                {
                    Children =
                    [
                        new ChildModel { Name = "" },
                        new ChildModel { Name = "" }
                    ]
                },
                Failure("Children[0].Name", "ChildName"),
                Failure("Children[1].Name", "ChildName")));
        await AssertSyncParity(
            "NativeForeach",
            mode => new NativeForeachItemValidator(Options(mode)),
            Invalid(
                static () => new ParentModel
                {
                    Children =
                    [
                        new ChildModel { Name = "" },
                        new ChildModel { Name = "ok" },
                        new ChildModel { Name = "" }
                    ]
                },
                Failure("Children[0].Name", "NativeChildName"),
                Failure("Children[2].Name", "NativeChildName")));
    }

    [Test]
    public async Task Composition_Attributes_And_Nested_Paths_Have_Mode_Parity()
    {
        await AssertSyncParity(
            "WholeModelComposition",
            mode =>
            {
                var options = Options(mode);
                return new CompositeConditionalValidator(new RequiredNameValidator(options), options);
            },
            Invalid(
                static () => new ConditionalModel { Name = "" },
                Failure("Name", "Required")),
            Valid(static () => new ConditionalModel { Name = "ok" }));
        await AssertSyncParity(
            "MultipleWholeModelComposition",
            mode =>
            {
                var options = Options(mode);
                return new MultipleCompositionValidator(
                    new RequiredFailureNameValidator(options),
                    new RequiredDescriptionValidator(options),
                    options);
            },
            Invalid(
                static () => new FailureBehaviorModel(),
                Failure("Name", "Required"),
                Failure("Description", "Required")));
        await AssertSyncParity(
            "WholeModelSeverityComposition",
            mode =>
            {
                var options = Options(mode);
                return new SeverityCompositionValidator(
                    new SeverityRulesValidator(options),
                    options);
            },
            ValidWithFailures(
                static () => new FailureBehaviorModel(),
                Failure("Name", "NameWarning", ValiantSeverity.Warning),
                Failure("Description", "DescriptionInfo", ValiantSeverity.Info)));
        await AssertSyncParity(
            "PropertyValidatorComposition",
            mode => new CustomerValidator(new AddressPropertyValidator(), Options(mode)),
            Valid(static () => new CustomerModel()),
            Invalid(
                static () => new CustomerModel
                {
                    Address = new AddressModel { Line1 = "", Zip = "" }
                },
                Failure("Address.Line1", "Required"),
                Failure("Address.Zip", "Required")));
        await AssertSyncParity(
            "AttributePropertyValidatorComposition",
            mode => new AttributeCustomerValidator(Options(mode)),
            Valid(static () => new AttributeCustomerModel()),
            Invalid(
                static () => new AttributeCustomerModel
                {
                    Address = new AddressModel { Line1 = "", Zip = "" }
                },
                Failure("Address.Line1", "Required"),
                Failure("Address.Zip", "Required")));
        await AssertSyncParity(
            "ChildProperty",
            mode => new ChildPropertyValidator(Options(mode)),
            Valid(static () => new CustomerModel()),
            Invalid(
                static () => new CustomerModel { Address = new AddressModel { Line1 = "" } },
                Failure("Address.Line1", "Required")));
        await AssertSyncParity(
            "Children",
            mode => new ChildrenValidator(Options(mode)),
            Valid(static () => new CustomerModel()),
            Invalid(
                static () => new CustomerModel
                {
                    Address = new AddressModel { Line1 = "", Zip = "" }
                },
                Failure("Address.Line1", "Required"),
                Failure("Address.Zip", "Required")));
        await AssertSyncParity(
            "ChildrenSeverityComposition",
            mode => new ChildrenSeverityValidator(Options(mode)),
            ValidWithFailures(
                static () => new CustomerModel
                {
                    Address = new AddressModel { Line1 = "", Zip = "" }
                },
                Failure("Address.Line1", "LineWarning", ValiantSeverity.Warning),
                Failure("Address.Zip", "ZipInfo", ValiantSeverity.Info)));
    }

    [Test]
    public async Task Failure_Behavior_Has_Snapshot_And_Execution_Parity()
    {
        await AssertSyncParity(
            "GlobalFailFast",
            mode => new GlobalFailureBehaviorValidator(Options(mode, ValiantFailureBehavior.FailFast)),
            Invalid(static () => new FailureBehaviorModel(), Failure("Name", "Required")));
        await AssertSyncParity(
            "ValidatorFailFastOverride",
            mode => new ValidatorFailFastBehaviorValidator(Options(mode)),
            Invalid(static () => new FailureBehaviorModel(), Failure("Name", "Required")));
        await AssertSyncParity(
            "ValidatorValidateAllOverride",
            mode => new ValidatorValidateAllBehaviorValidator(
                Options(mode, ValiantFailureBehavior.FailFast)),
            Invalid(
                static () => new FailureBehaviorModel(),
                Failure("Name", "Required"),
                Failure("Description", "Required")));
        await AssertSyncParity(
            "PropertyFailFast",
            mode => new PropertyChainFailFastBehaviorValidator(Options(mode)),
            Invalid(
                static () => new FailureBehaviorModel(),
                Failure("Name", "Required"),
                Failure("Description", "Required")));

        await AssertFailureProbeParity(
            "GlobalFailFastProbe",
            (mode, probe) => new GlobalFailFastProbeValidator(
                probe,
                Options(mode, ValiantFailureBehavior.FailFast)),
            expectedSameChainExecutions: 0,
            expectedFollowingExecutions: 0);
        await AssertFailureProbeParity(
            "ValidatorFailFastProbe",
            (mode, probe) => new ValidatorFailFastProbeValidator(probe, Options(mode)),
            expectedSameChainExecutions: 0,
            expectedFollowingExecutions: 0);
        await AssertFailureProbeParity(
            "ValidatorValidateAllProbe",
            (mode, probe) => new ValidatorValidateAllProbeValidator(
                probe,
                Options(mode, ValiantFailureBehavior.FailFast)),
            expectedSameChainExecutions: 0,
            expectedFollowingExecutions: 1);
        await AssertFailureProbeParity(
            "PropertyFailFastProbe",
            (mode, probe) => new PropertyFailFastProbeValidator(probe, Options(mode)),
            expectedSameChainExecutions: 0,
            expectedFollowingExecutions: 1);
    }

    [Test]
    public async Task Async_Rules_Composition_Guards_And_Ordering_Have_Mode_Parity()
    {
        await AssertAsyncParity(
            "SyncValidatorThroughAsyncApi",
            mode => new RequiredValidator(Options(mode)),
            Invalid(static () => new NullableTextModel(), Failure("Value", "Required")),
            Valid(static () => new NullableTextModel { Value = "ok" }));
        await AssertAsyncParity(
            "SatisfiesAsync",
            mode => new SatisfiesAsyncValidator(Options(mode)),
            Invalid(static () => new NumberModel { Value = 13 }, Failure("Value", "SatisfiesAsync")),
            Valid(static () => new NumberModel { Value = 12 }));
        await AssertAsyncParity(
            "SatisfiesAsyncWithModel",
            mode => new SatisfiesAsyncWithModelValidator(Options(mode)),
            Invalid(
                static () => new CompareModel { Left = 2, Right = 1 },
                Failure("Left", "SatisfiesAsync")),
            Valid(static () => new CompareModel { Left = 2, Right = 2 }));
        await AssertAsyncParity(
            "AsyncWholeModelComposition",
            mode =>
            {
                var options = Options(mode);
                return new CompositeAsyncConditionalValidator(
                    new AsyncRequiredNameValidator(options),
                    options);
            },
            Invalid(
                static () => new ConditionalModel { Name = "" },
                Failure("Name", "SatisfiesAsync")),
            Valid(static () => new ConditionalModel { Name = "ok" }));
        await AssertAsyncParity(
            "MixedSyncAsyncOrdering",
            mode => new MixedSyncAsyncValidator(Options(mode)),
            Invalid(
                static () => new FailureBehaviorModel(),
                Failure("Name", "SyncFirst"),
                Failure("Name", "AsyncSecond"),
                Failure("Description", "SyncLast")));
        await AssertAsyncParity(
            "SampleCatalogComposition",
            CreateSampleCatalogValidator,
            InvalidContaining(
                static () => SampleCatalogItemFactory.Invalid(),
                Failure("ShippingAddress.Line1", "Required"),
                Failure("Variants[0].Code", "Required"),
                Failure("AuditEntries[0].Actor", "Required")),
            InvalidContaining(
                static () => SampleCatalogItemFactory.Valid() with { Sku = "RESERVED" },
                Failure("Sku", "ReservedSku")));

        await AssertSyncGuardParity(
            mode => new SatisfiesAsyncValidator(Options(mode)),
            static () => new NumberModel { Value = 12 });
        await AssertCancellationParity();
    }

    [Test]
    public async Task Null_Inputs_Throw_Equivalent_Exceptions_For_All_APIs()
    {
        await AssertExceptionParity(
            "ValidateNull",
            mode => Task.FromResult(new RequiredValidator(Options(mode)).Validate(null!)));
        await AssertExceptionParity(
            "ValidateAsyncNull",
            async mode => await new RequiredValidator(Options(mode)).ValidateAsync(null!));
        await AssertExceptionParity(
            "IsValidNull",
            mode => Task.FromResult(new RequiredValidator(Options(mode)).IsValid(null!)));
        await AssertExceptionParity(
            "IsValidAsyncNull",
            async mode => await new RequiredValidator(Options(mode)).IsValidAsync(null!));
    }

    [Test]
    public async Task Explicit_And_Auto_Mode_Selection_Use_The_Generated_Dispatch()
    {
        await Assert.That(GetEffectiveMode(new RequiredValidator(Options(ValiantValidationMode.Fast))))
            .IsEqualTo(ValiantValidationMode.Fast);
        await Assert.That(GetEffectiveMode(new RequiredValidator(Options(ValiantValidationMode.Debuggable))))
            .IsEqualTo(ValiantValidationMode.Debuggable);

        var autoMode = GetEffectiveMode(new RequiredValidator(Options(ValiantValidationMode.Auto)));
#if DEBUG
        await Assert.That(autoMode).IsEqualTo(ValiantValidationMode.Debuggable);
#else
        await Assert.That(autoMode).IsEqualTo(ValiantValidationMode.Fast);
#endif
    }

    private static async Task AssertSyncParity<T>(
        string caseName,
        Func<ValiantValidationMode, IValidator<T>> createValidator,
        params ParityInput<T>[] inputs)
    {
        var fastValidator = createValidator(ValiantValidationMode.Fast);
        var debuggableValidator = createValidator(ValiantValidationMode.Debuggable);

        foreach (var input in inputs)
        {
            var fastResult = fastValidator.Validate(input.Create());
            var debuggableResult = debuggableValidator.Validate(input.Create());

            AssertEquivalentResults(caseName, fastResult, debuggableResult);
            AssertExpectedResult(caseName, fastResult, input);

            await Assert.That(fastValidator.IsValid(input.Create()))
                .IsEqualTo(debuggableValidator.IsValid(input.Create()));
            await Assert.That(fastValidator.IsValid(input.Create())).IsEqualTo(input.ExpectedValid);
        }
    }

    private static async Task AssertAsyncParity<T>(
        string caseName,
        Func<ValiantValidationMode, IValidator<T>> createValidator,
        params ParityInput<T>[] inputs)
    {
        var fastValidator = createValidator(ValiantValidationMode.Fast);
        var debuggableValidator = createValidator(ValiantValidationMode.Debuggable);

        foreach (var input in inputs)
        {
            var fastResult = await fastValidator.ValidateAsync(input.Create());
            var debuggableResult = await debuggableValidator.ValidateAsync(input.Create());

            AssertEquivalentResults(caseName, fastResult, debuggableResult);
            AssertExpectedResult(caseName, fastResult, input);

            await Assert.That(await fastValidator.IsValidAsync(input.Create()))
                .IsEqualTo(await debuggableValidator.IsValidAsync(input.Create()));
            await Assert.That(await fastValidator.IsValidAsync(input.Create())).IsEqualTo(input.ExpectedValid);
        }
    }

    private static void AssertEquivalentResults(
        string caseName,
        ValidationResult fast,
        ValidationResult debuggable)
    {
        var fastSnapshot = ResultSnapshot(fast);
        var debuggableSnapshot = ResultSnapshot(debuggable);
        if (!string.Equals(fastSnapshot, debuggableSnapshot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Validation mode parity failed for '{caseName}'.{Environment.NewLine}" +
                $"Fast:{Environment.NewLine}{fastSnapshot}{Environment.NewLine}" +
                $"Debuggable:{Environment.NewLine}{debuggableSnapshot}");
        }
    }

    private static void AssertExpectedResult<T>(
        string caseName,
        ValidationResult result,
        ParityInput<T> input)
    {
        if (result.IsValid != input.ExpectedValid)
        {
            throw new InvalidOperationException(
                $"Parity case '{caseName}' expected IsValid={input.ExpectedValid}, but received {result.IsValid}.");
        }

        var actual = result.Failures.Select(FailureIdentity).ToArray();
        var expected = input.ExpectedFailures.Select(ExpectedFailureIdentity).ToArray();
        var matches = input.ExactFailures
            ? actual.SequenceEqual(expected, StringComparer.Ordinal)
            : expected.All(item => actual.Contains(item, StringComparer.Ordinal));

        if (!matches)
        {
            throw new InvalidOperationException(
                $"Parity case '{caseName}' did not match its semantic oracle.{Environment.NewLine}" +
                $"Expected: {string.Join(", ", expected)}{Environment.NewLine}" +
                $"Actual: {string.Join(", ", actual)}");
        }

        foreach (var expectation in input.ExpectedFailures.Where(static item => item.Message is not null))
        {
            var failure = result.Failures.Single(item =>
                item.PropertyName == expectation.PropertyName &&
                item.ErrorCode == expectation.ErrorCode &&
                item.Severity == expectation.Severity);
            if (!string.Equals(failure.ErrorMessage, expectation.Message, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Parity case '{caseName}' expected message '{expectation.Message}', " +
                    $"but received '{failure.ErrorMessage}'.");
            }
        }
    }

    private static async Task AssertFailureProbeParity(
        string caseName,
        Func<ValiantValidationMode, FailureExecutionProbe, IValidator<FailureBehaviorModel>> createValidator,
        int expectedSameChainExecutions,
        int expectedFollowingExecutions)
    {
        var fastProbe = new FailureExecutionProbe();
        var debuggableProbe = new FailureExecutionProbe();
        var fastResult = createValidator(ValiantValidationMode.Fast, fastProbe)
            .Validate(new FailureBehaviorModel());
        var debuggableResult = createValidator(ValiantValidationMode.Debuggable, debuggableProbe)
            .Validate(new FailureBehaviorModel());

        AssertEquivalentResults(caseName, fastResult, debuggableResult);
        await Assert.That(fastProbe.SameChainExecutions).IsEqualTo(expectedSameChainExecutions);
        await Assert.That(debuggableProbe.SameChainExecutions).IsEqualTo(expectedSameChainExecutions);
        await Assert.That(fastProbe.FollowingExecutions).IsEqualTo(expectedFollowingExecutions);
        await Assert.That(debuggableProbe.FollowingExecutions).IsEqualTo(expectedFollowingExecutions);
    }

    private static async Task AssertSyncGuardParity<T>(
        Func<ValiantValidationMode, IValidator<T>> createValidator,
        Func<T> createInput)
    {
        var fastException = await CaptureExceptionAsync(
            () => Task.FromResult(createValidator(ValiantValidationMode.Fast).Validate(createInput())));
        var debuggableException = await CaptureExceptionAsync(
            () => Task.FromResult(createValidator(ValiantValidationMode.Debuggable).Validate(createInput())));

        await Assert.That(ExceptionSnapshot(fastException)).IsEqualTo(ExceptionSnapshot(debuggableException));
        await Assert.That(fastException).IsTypeOf<InvalidOperationException>();
        await Assert.That(fastException.Message).Contains("asynchronous");
    }

    private static async Task AssertExceptionParity(
        string caseName,
        Func<ValiantValidationMode, Task> action)
    {
        var fastException = await CaptureExceptionAsync(() => action(ValiantValidationMode.Fast));
        var debuggableException = await CaptureExceptionAsync(() => action(ValiantValidationMode.Debuggable));

        var fastSnapshot = ExceptionSnapshot(fastException);
        var debuggableSnapshot = ExceptionSnapshot(debuggableException);
        if (!string.Equals(fastSnapshot, debuggableSnapshot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Exception parity failed for '{caseName}'. Fast: {fastSnapshot}; " +
                $"Debuggable: {debuggableSnapshot}.");
        }

        await Assert.That(fastException).IsTypeOf<ArgumentNullException>();
        await Assert.That(((ArgumentNullException)fastException).ParamName).IsEqualTo("instance");
    }

    private static async Task AssertCancellationParity()
    {
        foreach (var mode in new[]
                 {
                     ValiantValidationMode.Fast,
                     ValiantValidationMode.Debuggable
                 })
        {
            var preCancelledProbe = new AsyncCancellationProbe();
            var preCancelledValidator = CreateCompositeCancellationValidator(mode, preCancelledProbe);
            using var preCancelled = new CancellationTokenSource();
            preCancelled.Cancel();
            var preCancelledException = await CaptureExceptionAsync(
                async () => await preCancelledValidator.ValidateAsync(
                    new NullableTextModel { Value = "value" },
                    preCancelled.Token));

            await Assert.That(preCancelledException).IsAssignableTo<OperationCanceledException>();
            await Assert.That(preCancelledProbe.Executions).IsEqualTo(0);

            var runningProbe = new AsyncCancellationProbe();
            var runningValidator = CreateCompositeCancellationValidator(mode, runningProbe);
            using var runningCancellation = new CancellationTokenSource();
            var validation = runningValidator.ValidateAsync(
                new NullableTextModel { Value = "value" },
                runningCancellation.Token).AsTask();

            await runningProbe.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            runningCancellation.Cancel();
            var runningException = await CaptureExceptionAsync(async () => await validation);

            await Assert.That(runningException).IsAssignableTo<OperationCanceledException>();
            await Assert.That(runningProbe.Executions).IsEqualTo(1);
        }
    }

    private static CompositeCancellationValidator CreateCompositeCancellationValidator(
        ValiantValidationMode mode,
        AsyncCancellationProbe probe)
    {
        var options = Options(mode);
        return new CompositeCancellationValidator(
            new CancellationAwareValidator(probe, options),
            options);
    }

    private static SampleCatalogItemValidator CreateSampleCatalogValidator(ValiantValidationMode mode)
    {
        var options = Options(mode);
        var addressValidator = new SampleAddressValidator(options);
        var addressPropertyValidator = new SampleAddressPropertyValidator(addressValidator);
        var rulesValidator = new SampleCatalogItemRulesValidator(addressPropertyValidator, options);
        return new SampleCatalogItemValidator(rulesValidator, options);
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

    private static ValiantValidationMode GetEffectiveMode(object validator)
    {
        var method = validator.GetType().GetMethod(
            "GetEffectiveValidationMode",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Generated mode selector was not found.");
        return (ValiantValidationMode)method.Invoke(validator, null)!;
    }

    private static string ResultSnapshot(ValidationResult result)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"IsValid:{result.IsValid}",
                $"Failures:{string.Join(";", result.Failures.Select(FailureSnapshot))}",
                $"Errors:{string.Join(";", result.Errors.Select(FailureSnapshot))}",
                $"Warnings:{string.Join(";", result.Warnings.Select(FailureSnapshot))}",
                $"Infos:{string.Join(";", result.Infos.Select(FailureSnapshot))}"
            ]);
    }

    private static string FailureSnapshot(ValidationFailure failure)
    {
        return $"{failure.PropertyName}|{failure.ErrorCode}|{failure.ErrorMessage}|{failure.Severity}";
    }

    private static string FailureIdentity(ValidationFailure failure)
    {
        return $"{failure.PropertyName}|{failure.ErrorCode}|{failure.Severity}";
    }

    private static string ExpectedFailureIdentity(FailureExpectation failure)
    {
        return $"{failure.PropertyName}|{failure.ErrorCode}|{failure.Severity}";
    }

    private static string ExceptionSnapshot(Exception exception)
    {
        return exception is ArgumentException argumentException
            ? $"{exception.GetType().FullName}|{argumentException.ParamName}|{exception.Message}"
            : $"{exception.GetType().FullName}|{exception.Message}";
    }

    private static ValiantValidationOptions Options(
        ValiantValidationMode mode,
        ValiantFailureBehavior failureBehavior = ValiantFailureBehavior.ValidateAll)
    {
        return new ValiantValidationOptions
        {
            Mode = mode,
            FailureBehavior = failureBehavior
        };
    }

    private static FailureExpectation Failure(
        string propertyName,
        string errorCode,
        ValiantSeverity severity = ValiantSeverity.Error)
    {
        return new FailureExpectation(propertyName, errorCode, severity, null);
    }

    private static ParityInput<T> Valid<T>(Func<T> create)
    {
        return new ParityInput<T>(create, true, true, []);
    }

    private static ParityInput<T> ValidWithFailures<T>(
        Func<T> create,
        params FailureExpectation[] failures)
    {
        return new ParityInput<T>(create, true, true, failures);
    }

    private static ParityInput<T> Invalid<T>(
        Func<T> create,
        params FailureExpectation[] failures)
    {
        return new ParityInput<T>(create, false, true, failures);
    }

    private static ParityInput<T> InvalidContaining<T>(
        Func<T> create,
        params FailureExpectation[] failures)
    {
        return new ParityInput<T>(create, false, false, failures);
    }

    private sealed record ParityInput<T>(
        Func<T> Create,
        bool ExpectedValid,
        bool ExactFailures,
        IReadOnlyList<FailureExpectation> ExpectedFailures);

    private sealed record FailureExpectation(
        string PropertyName,
        string ErrorCode,
        ValiantSeverity Severity,
        string? Message);
}

public sealed class CultureDecimalModel
{
    public decimal Value { get; init; }
}

[ValiantValidator]
public sealed partial class CultureDecimalValidator : ValidatorDefinition<CultureDecimalModel>
{
    public override void Configure(ValidationContext<CultureDecimalModel> context)
    {
        context.Property(model => model.Value).GreaterThan(1234.5m);
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class RequiredFailureNameValidator : ValidatorDefinition<FailureBehaviorModel>
{
    public override void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.Property(model => model.Name).Required();
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class RequiredDescriptionValidator : ValidatorDefinition<FailureBehaviorModel>
{
    public override void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.Property(model => model.Description).Required();
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class MultipleCompositionValidator(
    RequiredFailureNameValidator nameValidator,
    RequiredDescriptionValidator descriptionValidator) : ValidatorDefinition<FailureBehaviorModel>
{
    public override void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.UseValidator(nameValidator);
        context.UseValidator(descriptionValidator);
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class SeverityRulesValidator : ValidatorDefinition<FailureBehaviorModel>
{
    public override void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.Property(model => model.Name)
            .Required(code: "NameWarning", severity: ValiantSeverity.Warning);
        context.Property(model => model.Description)
            .Required(code: "DescriptionInfo", severity: ValiantSeverity.Info);
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class SeverityCompositionValidator(
    SeverityRulesValidator rulesValidator) : ValidatorDefinition<FailureBehaviorModel>
{
    public override void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.UseValidator(rulesValidator);
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class CollectionSeverityValidator : ValidatorDefinition<ParentModel>
{
    public override void Configure(ValidationContext<ParentModel> context)
    {
        context.Property(model => model.Children).ForEach(child =>
        {
            child.Property(model => model.Name)
                .Required(code: "ChildWarning", severity: ValiantSeverity.Warning);
        });
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class ChildrenSeverityValidator : ValidatorDefinition<CustomerModel>
{
    public override void Configure(ValidationContext<CustomerModel> context)
    {
        context.Property(model => model.Address).Children(child =>
        {
            child.Property(model => model.Line1)
                .Required(code: "LineWarning", severity: ValiantSeverity.Warning);
            child.Property(model => model.Zip)
                .Required(code: "ZipInfo", severity: ValiantSeverity.Info);
        });
    }
}

public sealed class FailureExecutionProbe
{
    public int SameChainExecutions { get; private set; }

    public int FollowingExecutions { get; private set; }

    public bool RecordSameChain()
    {
        SameChainExecutions++;
        return false;
    }

    public bool RecordFollowing()
    {
        FollowingExecutions++;
        return false;
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class GlobalFailFastProbeValidator(
    FailureExecutionProbe probe) : ValidatorDefinition<FailureBehaviorModel>
{
    public override void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.Property(model => model.Name).Required();
        context.Property(model => model.Description)
            .Satisfies(_ => probe.RecordFollowing(), code: "Following");
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class ValidatorFailFastProbeValidator(
    FailureExecutionProbe probe) : ValidatorDefinition<FailureBehaviorModel>
{
    public override void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.FailFast();
        context.Property(model => model.Name).Required();
        context.Property(model => model.Description)
            .Satisfies(_ => probe.RecordFollowing(), code: "Following");
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class ValidatorValidateAllProbeValidator(
    FailureExecutionProbe probe) : ValidatorDefinition<FailureBehaviorModel>
{
    public override void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.ValidateAll();
        context.Property(model => model.Name).Required();
        context.Property(model => model.Description)
            .Satisfies(_ => probe.RecordFollowing(), code: "Following");
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class PropertyFailFastProbeValidator(
    FailureExecutionProbe probe) : ValidatorDefinition<FailureBehaviorModel>
{
    public override void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.Property(model => model.Name)
            .FailFast()
            .Required()
            .Satisfies(_ => probe.RecordSameChain(), code: "SameChain");
        context.Property(model => model.Description)
            .Satisfies(_ => probe.RecordFollowing(), code: "Following");
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class MixedSyncAsyncValidator : ValidatorDefinition<FailureBehaviorModel>
{
    public override void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.Property(model => model.Name)
            .Required(code: "SyncFirst")
            .SatisfiesAsync(
                static (_, _) => new ValueTask<bool>(false),
                code: "AsyncSecond");
        context.Property(model => model.Description)
            .Required(code: "SyncLast");
    }
}

public sealed class AsyncCancellationProbe
{
    public TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Executions { get; private set; }

    public async ValueTask<bool> ValidateAsync(
        string? value,
        CancellationToken cancellationToken)
    {
        _ = value;
        Executions++;
        Started.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return true;
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class CancellationAwareValidator(
    AsyncCancellationProbe probe) : ValidatorDefinition<NullableTextModel>
{
    public override void Configure(ValidationContext<NullableTextModel> context)
    {
        context.Property(model => model.Value)
            .SatisfiesAsync(probe.ValidateAsync, code: "CancellationAware");
    }
}

[ValiantValidator(RegisterAsDefault = false)]
public sealed partial class CompositeCancellationValidator(
    CancellationAwareValidator innerValidator) : ValidatorDefinition<NullableTextModel>
{
    public override void Configure(ValidationContext<NullableTextModel> context)
    {
        context.UseValidator(innerValidator);
    }
}
