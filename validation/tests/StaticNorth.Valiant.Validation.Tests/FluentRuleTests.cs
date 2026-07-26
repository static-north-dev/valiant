namespace StaticNorth.Valiant.Validation.Tests;

public sealed class FluentRuleTests
{
    [Test]
    public async Task ValidationResult_Snapshots_Failures()
    {
        var failures = new List<ValidationFailure>
        {
            new("Name", "Required", "Name is required.")
        };

        var result = new ValidationResult(failures);
        failures.Add(new ValidationFailure("Name", "Warning", "Name warning.", ValiantSeverity.Warning));

        await Assert.That(result.Failures).Count().IsEqualTo(1);
        await Assert.That(result.Errors).Count().IsEqualTo(1);
        await Assert.That(result.Warnings).IsEmpty();
    }

    [Test]
    public async Task ValidationOptions_Default_Lifetime_Is_Scoped()
    {
        await Assert.That(new ValiantValidationOptions().Lifetime).IsEqualTo(ValiantValidatorLifetime.Scoped);
    }

    [Test]
    public async Task NotNull_Fails_For_Null()
    {
        var result = new NotNullValidator().Validate(new NullableTextModel());

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("NotNull");
    }

    [Test]
    public async Task Null_Fails_For_Non_Null()
    {
        var result = new NullValidator().Validate(new NullableTextModel { Value = "x" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Null");
    }

    [Test]
    public async Task Required_Fails_For_Empty_String()
    {
        var result = new RequiredValidator().Validate(new NullableTextModel { Value = "" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Required");
    }

    [Test]
    public async Task Required_Fails_For_Whitespace_String()
    {
        var result = new RequiredValidator().Validate(new NullableTextModel { Value = "   " });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Required");
    }

    [Test]
    public async Task NotEmpty_Fails_For_Empty_String()
    {
        var result = new NotEmptyValidator().Validate(new NullableTextModel { Value = "" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("NotEmpty");
    }

    [Test]
    public async Task NotEmpty_Allows_Whitespace_String()
    {
        var result = new NotEmptyValidator().Validate(new NullableTextModel { Value = "   " });

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Empty_Fails_For_Non_Empty_String()
    {
        var result = new EmptyValidator().Validate(new NullableTextModel { Value = "x" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Empty");
    }

    [Test]
    public async Task LengthBetween_Fails_When_String_Is_Outside_Range()
    {
        var result = new LengthBetweenValidator().Validate(new NullableTextModel { Value = "abcd" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("LengthBetween");
    }

    [Test]
    public async Task MinLength_Fails_When_String_Is_Too_Short()
    {
        var result = new MinLengthValidator().Validate(new NullableTextModel { Value = "a" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("MinLength");
    }

    [Test]
    public async Task MaxLength_Fails_When_String_Is_Too_Long()
    {
        var result = new MaxLengthValidator().Validate(new NullableTextModel { Value = "abcd" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("MaxLength");
    }

    [Test]
    public async Task MaxLength_Fails_For_Non_Nullable_String_When_String_Is_Too_Long()
    {
        var result = new NonNullableStringMaxLengthValidator().Validate(new NonNullableTextModel("abcd"));

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("MaxLength");
    }

    [Test]
    public async Task GreaterThan_Fails_When_Value_Is_Equal()
    {
        var result = new GreaterThanValidator().Validate(new NumberModel { Value = 10 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("GreaterThan");
    }

    [Test]
    public async Task GreaterThan_Allows_Nullable_Comparable_Values()
    {
        var fastValidator = new NullableDecimalGreaterThanValidator(FastOptions());
        var debugValidator = new NullableDecimalGreaterThanValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Debuggable });

        var fastNullResult = fastValidator.Validate(new NullableDecimalModel());
        var fastEqualResult = fastValidator.Validate(new NullableDecimalModel { Value = 0m });
        var debugNullResult = debugValidator.Validate(new NullableDecimalModel());
        var debugEqualResult = debugValidator.Validate(new NullableDecimalModel { Value = 0m });

        await Assert.That(fastNullResult.IsValid).IsTrue();
        await Assert.That(debugNullResult.IsValid).IsTrue();
        await Assert.That(fastEqualResult.Errors[0].ErrorCode).IsEqualTo("GreaterThan");
        await Assert.That(debugEqualResult.Errors[0].ErrorCode).IsEqualTo("GreaterThan");
    }

    [Test]
    public async Task GreaterThan_Allows_Nullable_Byte_And_SByte_Values()
    {
        var fastValidator = new NullableByteGreaterThanValidator(FastOptions());
        var debugValidator = new NullableByteGreaterThanValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Debuggable });

        var fastNullResult = fastValidator.Validate(new NullableByteModel());
        var fastEqualResult = fastValidator.Validate(new NullableByteModel { Byte = 0, SByte = 0 });
        var debugNullResult = debugValidator.Validate(new NullableByteModel());
        var debugEqualResult = debugValidator.Validate(new NullableByteModel { Byte = 0, SByte = 0 });

        await Assert.That(fastNullResult.IsValid).IsTrue();
        await Assert.That(debugNullResult.IsValid).IsTrue();
        await Assert.That(fastEqualResult.Errors.Select(static error => error.PropertyName)).IsEquivalentTo(["Byte", "SByte"]);
        await Assert.That(debugEqualResult.Errors.Select(static error => error.PropertyName)).IsEquivalentTo(["Byte", "SByte"]);
    }

    [Test]
    public async Task GreaterThanOrEqualTo_Fails_When_Value_Is_Less()
    {
        var result = new GreaterThanOrEqualToValidator().Validate(new NumberModel { Value = 9 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("GreaterThanOrEqualTo");
    }

    [Test]
    public async Task LessThan_Fails_When_Value_Is_Equal()
    {
        var result = new LessThanValidator().Validate(new NumberModel { Value = 10 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("LessThan");
    }

    [Test]
    public async Task LessThanOrEqualTo_Fails_When_Value_Is_Greater()
    {
        var result = new LessThanOrEqualToValidator().Validate(new NumberModel { Value = 11 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("LessThanOrEqualTo");
    }

    [Test]
    public async Task Equal_Fails_When_Value_Does_Not_Match()
    {
        var result = new EqualValidator().Validate(new NumberModel { Value = 11 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Equal");
    }

    [Test]
    public async Task NotEqual_Fails_When_Value_Matches()
    {
        var result = new NotEqualValidator().Validate(new NumberModel { Value = 10 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("NotEqual");
    }

    [Test]
    public async Task InclusiveBetween_Fails_When_Value_Is_Outside_Range()
    {
        var result = new InclusiveBetweenValidator().Validate(new NumberModel { Value = 4 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("InclusiveBetween");
    }

    [Test]
    public async Task ExclusiveBetween_Fails_When_Value_Is_On_Boundary()
    {
        var result = new ExclusiveBetweenValidator().Validate(new NumberModel { Value = 5 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("ExclusiveBetween");
    }

    [Test]
    public async Task Satisfies_Fails_When_Predicate_Returns_False()
    {
        var result = new SatisfiesValidator().Validate(new NullableTextModel { Value = "!" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Satisfies");
    }

    [Test]
    public async Task Satisfies_With_Model_And_Property_Fails_When_Predicate_Returns_False()
    {
        var result = new SatisfiesWithModelValidator().Validate(new CompareModel { Left = 1, Right = 2 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Satisfies");
    }

    [Test]
    public async Task SatisfiesAsync_Fails_When_Predicate_Returns_False()
    {
        var result = await new SatisfiesAsyncValidator().ValidateAsync(new NumberModel { Value = 13 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("SatisfiesAsync");
    }

    [Test]
    public async Task SatisfiesAsync_With_Model_And_Property_Fails_When_Predicate_Returns_False()
    {
        var result = await new SatisfiesAsyncWithModelValidator().ValidateAsync(new CompareModel { Left = 2, Right = 1 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("SatisfiesAsync");
    }

    [Test]
    public async Task Check_Metadata_Replaces_Default_Code_And_Message()
    {
        var result = new MetadataValidator().Validate(new NullableTextModel { Value = "" });

        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("ValueRequired");
        await Assert.That(result.Errors[0].ErrorMessage).IsEqualTo("Custom message.");
    }

    [Test]
    public async Task Severity_Warning_Does_Not_Make_Result_Invalid()
    {
        var fastResult = new WarningSeverityValidator(FastOptions()).Validate(new NullableTextModel());
        var debugResult = new WarningSeverityValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Debuggable }).Validate(new NullableTextModel());

        await Assert.That(fastResult.IsValid).IsTrue();
        await Assert.That(debugResult.IsValid).IsTrue();
        await Assert.That(fastResult.Failures).Count().IsEqualTo(1);
        await Assert.That(debugResult.Failures).Count().IsEqualTo(1);
        await Assert.That(fastResult.Errors).IsEmpty();
        await Assert.That(debugResult.Errors).IsEmpty();
        await Assert.That(fastResult.Warnings[0].Severity).IsEqualTo(ValiantSeverity.Warning);
        await Assert.That(debugResult.Warnings[0].Severity).IsEqualTo(ValiantSeverity.Warning);
    }

    [Test]
    public async Task ThrowIfFailed_Throws_Only_When_Result_Has_Errors()
    {
        var validWithWarning = new WarningSeverityValidator(FastOptions()).Validate(new NullableTextModel());
        var failed = new RequiredValidator(FastOptions()).Validate(new NullableTextModel());

        await Assert.That(validWithWarning.ThrowIfFailed()).IsSameReferenceAs(validWithWarning);
        var exception = await Assert.ThrowsAsync<ValiantValidationException>(() => Task.FromResult(failed.ThrowIfFailed()));
        await Assert.That(exception!.Result).IsSameReferenceAs(failed);
        await Assert.That(exception.Errors[0].ErrorCode).IsEqualTo("Required");
    }

    [Test]
    public async Task IsValid_Returns_True_For_Sync_Only_Valid_Model()
    {
        var isValid = new RequiredValidator().IsValid(new NullableTextModel { Value = "ok" });

        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task IsValidAsync_Runs_Sync_And_Async_Rules()
    {
        var isValid = await new SatisfiesAsyncValidator().IsValidAsync(new NumberModel { Value = 12 });

        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task Validate_Throws_For_Validator_With_Async_Rules()
    {
        var validator = new SatisfiesAsyncValidator();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.FromResult(validator.Validate(new NumberModel { Value = 12 })));
    }

    [Test]
    public async Task If_Else_Condition_Runs_Only_Selected_Rules()
    {
        var validator = new IfElseConditionalValidator();

        var nameResult = validator.Validate(new ConditionalModel { ValidateName = true, Name = "", Age = 0 });
        var ageResult = validator.Validate(new ConditionalModel { ValidateName = false, Name = "", Age = 17 });
        var skippedAgeResult = validator.Validate(new ConditionalModel { ValidateName = true, Name = "ok", Age = 17 });

        await Assert.That(nameResult.Errors[0].ErrorCode).IsEqualTo("Required");
        await Assert.That(ageResult.Errors[0].ErrorCode).IsEqualTo("GreaterThanOrEqualTo");
        await Assert.That(skippedAgeResult.IsValid).IsTrue();
    }

    [Test]
    public async Task Switch_Condition_Runs_Selected_Section_Rules()
    {
        var validator = new SwitchConditionalValidator();

        var nameResult = validator.Validate(new ConditionalModel { Mode = ConditionalMode.Name, Name = "" });
        var ageResult = validator.Validate(new ConditionalModel { Mode = ConditionalMode.Age, RequireAdult = true, Age = 17 });
        var skippedWhenResult = validator.Validate(new ConditionalModel { Mode = ConditionalMode.Age, RequireAdult = false, Age = 17 });

        await Assert.That(nameResult.Errors[0].ErrorCode).IsEqualTo("Required");
        await Assert.That(ageResult.Errors[0].ErrorCode).IsEqualTo("GreaterThanOrEqualTo");
        await Assert.That(skippedWhenResult.IsValid).IsTrue();
    }

    [Test]
    public async Task Property_When_Condition_Runs_Only_When_Predicate_Matches()
    {
        var fastValidator = new ConditionalWhenValidator(FastOptions());
        var debugValidator = new ConditionalWhenValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Debuggable });

        var fastSkippedResult = fastValidator.Validate(new ConditionalModel { RequireAdult = false, Age = 17 });
        var fastFailedResult = fastValidator.Validate(new ConditionalModel { RequireAdult = true, Age = 17 });
        var debugSkippedResult = debugValidator.Validate(new ConditionalModel { RequireAdult = false, Age = 17 });
        var debugFailedResult = debugValidator.Validate(new ConditionalModel { RequireAdult = true, Age = 17 });

        await Assert.That(fastSkippedResult.IsValid).IsTrue();
        await Assert.That(debugSkippedResult.IsValid).IsTrue();
        await Assert.That(fastFailedResult.Errors[0].ErrorCode).IsEqualTo("GreaterThanOrEqualTo");
        await Assert.That(debugFailedResult.Errors[0].ErrorCode).IsEqualTo("GreaterThanOrEqualTo");
    }

    [Test]
    public async Task UseValidator_Reuses_Whole_Model_Validator()
    {
        var validator = new CompositeConditionalValidator(new RequiredNameValidator());

        var result = validator.Validate(new ConditionalModel { Name = "" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Required");
    }

    [Test]
    public async Task UseValidator_ValidateAsync_Runs_Async_Reused_Validator()
    {
        var validator = new CompositeAsyncConditionalValidator(new AsyncRequiredNameValidator());

        var result = await validator.ValidateAsync(new ConditionalModel { Name = "" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("SatisfiesAsync");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.FromResult(validator.Validate(new ConditionalModel { Name = "" })));
    }

    [Test]
    public async Task DefinedEnum_Fails_For_Undefined_Enum_Value()
    {
        var validator = new DefinedEnumValidator(FastOptions());

        var result = validator.Validate(new EnumValidationModel { State = (SampleState)99 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("DefinedEnum");
    }

    [Test]
    public async Task EnumName_Fails_For_Unknown_String_Name()
    {
        var validator = new EnumNameValidator(FastOptions());

        var result = validator.Validate(new EnumValidationModel { StateName = "Missing" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("EnumName");
    }

    [Test]
    public async Task EnumValue_Fails_For_Unknown_Integer_Value()
    {
        var validator = new EnumValueValidator(FastOptions());

        var result = validator.Validate(new EnumValidationModel { StateValue = 99 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("EnumValue");
    }

    [Test]
    public async Task NotEmpty_Fails_For_Empty_Collection()
    {
        var validator = new NotEmptyCollectionValidator(FastOptions());

        var result = validator.Validate(new CollectionModel { Values = [] });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("NotEmpty");
    }

    [Test]
    public async Task Collection_Count_Checks_Fail_When_Count_Is_Outside_Limits()
    {
        var countValidator = new CountCollectionValidator(FastOptions());
        var betweenValidator = new CountBetweenCollectionValidator(FastOptions());
        var minValidator = new MinCountCollectionValidator(FastOptions());
        var maxValidator = new MaxCountCollectionValidator(FastOptions());

        var countResult = countValidator.Validate(new CollectionModel { Values = [1, 2] });
        var betweenResult = betweenValidator.Validate(new CollectionModel { Values = [1, 2, 3, 4] });
        var minResult = minValidator.Validate(new CollectionModel { Values = [1] });
        var maxResult = maxValidator.Validate(new CollectionModel { Values = [1, 2, 3, 4] });

        await Assert.That(countResult.Errors[0].ErrorCode).IsEqualTo("Count");
        await Assert.That(betweenResult.Errors[0].ErrorCode).IsEqualTo("CountBetween");
        await Assert.That(minResult.Errors[0].ErrorCode).IsEqualTo("MinCount");
        await Assert.That(maxResult.Errors[0].ErrorCode).IsEqualTo("MaxCount");
    }

    [Test]
    public async Task Collection_Length_Checks_Fail_When_Length_Is_Outside_Limits()
    {
        var betweenValidator = new LengthBetweenCollectionValidator(FastOptions());
        var minValidator = new MinLengthCollectionValidator(FastOptions());
        var maxValidator = new MaxLengthCollectionValidator(FastOptions());

        var betweenResult = betweenValidator.Validate(new CollectionModel { Values = [1, 2, 3, 4] });
        var minResult = minValidator.Validate(new CollectionModel { Values = [1] });
        var maxResult = maxValidator.Validate(new CollectionModel { Values = [1, 2, 3, 4] });

        await Assert.That(betweenResult.Errors[0].ErrorCode).IsEqualTo("LengthBetween");
        await Assert.That(minResult.Errors[0].ErrorCode).IsEqualTo("MinLength");
        await Assert.That(maxResult.Errors[0].ErrorCode).IsEqualTo("MaxLength");
    }

    [Test]
    public async Task Collection_Item_Checks_Fail_When_Item_Presence_Does_Not_Match()
    {
        var containsValidator = new ContainsCollectionValidator(FastOptions());
        var excludesValidator = new ExcludesCollectionValidator(FastOptions());

        var containsResult = containsValidator.Validate(new CollectionModel { Values = [1, 2] });
        var excludesResult = excludesValidator.Validate(new CollectionModel { Values = [1, 2, 99] });

        await Assert.That(containsResult.Errors[0].ErrorCode).IsEqualTo("Contains");
        await Assert.That(excludesResult.Errors[0].ErrorCode).IsEqualTo("Excludes");
    }

    [Test]
    public async Task Dictionary_Checks_Fail_When_Key_Or_Value_Is_Missing()
    {
        var keyValidator = new HasKeyDictionaryValidator(FastOptions());
        var valueValidator = new HasValueDictionaryValidator(FastOptions());

        var keyResult = keyValidator.Validate(new DictionaryModel { Values = new Dictionary<string, int> { ["other"] = 7 } });
        var valueResult = valueValidator.Validate(new DictionaryModel { Values = new Dictionary<string, int> { ["id"] = 1 } });

        await Assert.That(keyResult.Errors[0].ErrorCode).IsEqualTo("HasKey");
        await Assert.That(valueResult.Errors[0].ErrorCode).IsEqualTo("HasValue");
    }

    [Test]
    public async Task Collection_Predicate_Checks_Fail_When_Predicate_Does_Not_Match()
    {
        var allValidator = new AllCollectionValidator(FastOptions());
        var anyValidator = new AnyCollectionValidator(FastOptions());
        var noneValidator = new NoneCollectionValidator(FastOptions());

        var allResult = allValidator.Validate(new CollectionModel { Values = [1, -1] });
        var anyResult = anyValidator.Validate(new CollectionModel { Values = [1, 2] });
        var noneResult = noneValidator.Validate(new CollectionModel { Values = [1, -1] });

        await Assert.That(allResult.Errors[0].ErrorCode).IsEqualTo("All");
        await Assert.That(anyResult.Errors[0].ErrorCode).IsEqualTo("Any");
        await Assert.That(noneResult.Errors[0].ErrorCode).IsEqualTo("None");
    }

    [Test]
    public async Task Collection_ForEach_Validates_Each_Item_With_Property_Path()
    {
        var validator = new ChildCollectionValidator(FastOptions());

        var result = validator.Validate(new ParentModel
        {
            Children =
            [
                new ChildModel { Name = "ok" },
                new ChildModel { Name = "" }
            ]
        });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].PropertyName).IsEqualTo("Children[1].Name");
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Required");
    }

    [Test]
    public async Task Collection_ForEach_Can_Use_Full_Collection_Parameter()
    {
        var validator = new ChildCollectionWithCollectionParameterValidator(FastOptions());

        var result = validator.Validate(new ParentModel
        {
            Children = [new ChildModel { Name = "" }]
        });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].PropertyName).IsEqualTo("Children[0].Name");
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("ChildName");
    }

    [Test]
    public async Task Native_Foreach_Can_Validate_Specific_Item()
    {
        var model = new ParentModel
        {
            Children =
            [
                new ChildModel { Name = "ok" },
                new ChildModel { Name = "" }
            ]
        };

        var fastResult = new NativeForeachItemValidator(FastOptions()).Validate(model);
        var debugResult = new NativeForeachItemValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Debuggable }).Validate(model);

        await Assert.That(fastResult.IsValid).IsFalse();
        await Assert.That(debugResult.IsValid).IsFalse();
        await Assert.That(fastResult.Errors[0].PropertyName).IsEqualTo("Children[1].Name");
        await Assert.That(debugResult.Errors[0].PropertyName).IsEqualTo("Children[1].Name");
        await Assert.That(fastResult.Errors[0].ErrorCode).IsEqualTo("NativeChildName");
        await Assert.That(debugResult.Errors[0].ErrorCode).IsEqualTo("NativeChildName");
    }

    [Test]
    public async Task Global_FailFast_Stops_After_First_Failure()
    {
        var fastValidator = new GlobalFailureBehaviorValidator(new ValiantValidationOptions
        {
            Mode = ValiantValidationMode.Fast,
            FailureBehavior = ValiantFailureBehavior.FailFast
        });
        var debugValidator = new GlobalFailureBehaviorValidator(new ValiantValidationOptions
        {
            Mode = ValiantValidationMode.Debuggable,
            FailureBehavior = ValiantFailureBehavior.FailFast
        });

        var fastResult = fastValidator.Validate(new FailureBehaviorModel());
        var debugResult = debugValidator.Validate(new FailureBehaviorModel());

        await Assert.That(fastResult.Errors).Count().IsEqualTo(1);
        await Assert.That(debugResult.Errors).Count().IsEqualTo(1);
        await Assert.That(fastResult.Errors[0].PropertyName).IsEqualTo("Name");
        await Assert.That(debugResult.Errors[0].PropertyName).IsEqualTo("Name");
    }

    [Test]
    public async Task Validator_FailFast_Overrides_Global_ValidateAll()
    {
        var fastValidator = new ValidatorFailFastBehaviorValidator(FastOptions());
        var debugValidator = new ValidatorFailFastBehaviorValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Debuggable });

        var fastResult = fastValidator.Validate(new FailureBehaviorModel());
        var debugResult = debugValidator.Validate(new FailureBehaviorModel());

        await Assert.That(fastResult.Errors).Count().IsEqualTo(1);
        await Assert.That(debugResult.Errors).Count().IsEqualTo(1);
        await Assert.That(fastResult.Errors[0].PropertyName).IsEqualTo("Name");
        await Assert.That(debugResult.Errors[0].PropertyName).IsEqualTo("Name");
    }

    [Test]
    public async Task Validator_ValidateAll_Overrides_Global_FailFast()
    {
        var fastValidator = new ValidatorValidateAllBehaviorValidator(new ValiantValidationOptions
        {
            Mode = ValiantValidationMode.Fast,
            FailureBehavior = ValiantFailureBehavior.FailFast
        });
        var debugValidator = new ValidatorValidateAllBehaviorValidator(new ValiantValidationOptions
        {
            Mode = ValiantValidationMode.Debuggable,
            FailureBehavior = ValiantFailureBehavior.FailFast
        });

        var fastResult = fastValidator.Validate(new FailureBehaviorModel());
        var debugResult = debugValidator.Validate(new FailureBehaviorModel());

        await Assert.That(fastResult.Errors).Count().IsEqualTo(2);
        await Assert.That(debugResult.Errors).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Property_Chain_FailFast_Stops_Only_That_Property_Chain()
    {
        var fastResult = new PropertyChainFailFastBehaviorValidator(FastOptions()).Validate(new FailureBehaviorModel());
        var debugResult = new PropertyChainFailFastBehaviorValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Debuggable }).Validate(new FailureBehaviorModel());

        await Assert.That(fastResult.Errors.Select(static error => error.ErrorCode)).IsEquivalentTo(["Required", "Required"]);
        await Assert.That(debugResult.Errors.Select(static error => error.ErrorCode)).IsEquivalentTo(["Required", "Required"]);
    }

    [Test]
    public async Task Property_UseValidator_Reuses_Property_Validator_With_Prefixed_Failures()
    {
        var addressValidator = new AddressPropertyValidator();
        var fastResult = new CustomerValidator(addressValidator, FastOptions()).Validate(new CustomerModel
        {
            Address = new AddressModel { Line1 = "", Zip = "" }
        });
        var debugResult = new CustomerValidator(
            addressValidator,
            new ValiantValidationOptions { Mode = ValiantValidationMode.Debuggable }).Validate(new CustomerModel
            {
                Address = new AddressModel { Line1 = "", Zip = "" }
            });

        await Assert.That(fastResult.Errors.Select(static error => error.PropertyName)).IsEquivalentTo(["Address.Line1", "Address.Zip"]);
        await Assert.That(debugResult.Errors.Select(static error => error.PropertyName)).IsEquivalentTo(["Address.Line1", "Address.Zip"]);
    }

    [Test]
    public async Task Property_UseValidator_Skips_Null_Property_Value()
    {
        var validator = new CustomerValidator(new AddressPropertyValidator(), FastOptions());

        var result = validator.Validate(new CustomerModel());

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task PropertyValidatorDefinition_Allows_Sync_Or_Async_Override()
    {
        var syncOnlyResult = await new AddressPropertyValidator().ValidateAsync(
            new AddressModel { Line1 = "", Zip = "" });
        var asyncOnlyResult = new AsyncOnlyAddressPropertyValidator().Validate(
            new AddressModel { Line1 = "", Zip = "" });

        await Assert.That(syncOnlyResult.Errors.Select(static error => error.PropertyName)).IsEquivalentTo(["Line1", "Zip"]);
        await Assert.That(asyncOnlyResult.Errors.Select(static error => error.PropertyName)).IsEquivalentTo(["Line1", "Zip"]);
    }

    [Test]
    public async Task Attribute_UseValidator_Reuses_Property_Validator_With_Prefixed_Failures()
    {
        var result = new AttributeCustomerValidator(FastOptions()).Validate(new AttributeCustomerModel
        {
            Address = new AddressModel { Line1 = "", Zip = "" }
        });

        await Assert.That(result.Errors.Select(static error => error.PropertyName)).IsEquivalentTo(["Address.Line1", "Address.Zip"]);
    }

    [Test]
    public async Task Child_Property_Rule_Uses_Full_Path_And_Skips_Null_Parent()
    {
        var fastValidator = new ChildPropertyValidator(FastOptions());
        var debugValidator = new ChildPropertyValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Debuggable });

        var fastNullParentResult = fastValidator.Validate(new CustomerModel());
        var debugNullParentResult = debugValidator.Validate(new CustomerModel());
        var fastInvalidChildResult = fastValidator.Validate(new CustomerModel { Address = new AddressModel { Line1 = "" } });
        var debugInvalidChildResult = debugValidator.Validate(new CustomerModel { Address = new AddressModel { Line1 = "" } });

        await Assert.That(fastNullParentResult.IsValid).IsTrue();
        await Assert.That(debugNullParentResult.IsValid).IsTrue();
        await Assert.That(fastInvalidChildResult.Errors[0].PropertyName).IsEqualTo("Address.Line1");
        await Assert.That(debugInvalidChildResult.Errors[0].PropertyName).IsEqualTo("Address.Line1");
        await Assert.That(fastInvalidChildResult.Errors[0].ErrorCode).IsEqualTo("Required");
        await Assert.That(debugInvalidChildResult.Errors[0].ErrorCode).IsEqualTo("Required");
    }

    [Test]
    public async Task Children_Rule_Validates_Child_Context_With_Whole_Model()
    {
        var fastValidator = new ChildrenValidator(FastOptions());
        var debugValidator = new ChildrenValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Debuggable });

        var fastNullChildResult = fastValidator.Validate(new CustomerModel());
        var debugNullChildResult = debugValidator.Validate(new CustomerModel());
        var fastInvalidChildResult = fastValidator.Validate(new CustomerModel { Address = new AddressModel { Line1 = "", Zip = "" } });
        var debugInvalidChildResult = debugValidator.Validate(new CustomerModel { Address = new AddressModel { Line1 = "", Zip = "" } });

        await Assert.That(fastNullChildResult.IsValid).IsTrue();
        await Assert.That(debugNullChildResult.IsValid).IsTrue();
        await Assert.That(fastInvalidChildResult.Errors.Select(static error => error.PropertyName)).IsEquivalentTo(["Address.Line1", "Address.Zip"]);
        await Assert.That(debugInvalidChildResult.Errors.Select(static error => error.PropertyName)).IsEquivalentTo(["Address.Line1", "Address.Zip"]);
    }

    [Test]
    public async Task Configure_Loops_Run_In_Fast_Mode()
    {
        var validator = new LoopValidator(FastOptions());

        var result = validator.Validate(new LoopModel
        {
            Values = [-1],
            Name = "",
            RunWhile = true,
            RunDo = true
        });

        var codes = result.Errors.Select(static error => error.ErrorCode).ToArray();
        await Assert.That(codes).Contains("ForeachName");
        await Assert.That(codes).Contains("ForName");
        await Assert.That(codes).Contains("WhileName");
        await Assert.That(codes).Contains("DoName");
    }

    private static ValiantValidationOptions FastOptions()
    {
        return new ValiantValidationOptions { Mode = ValiantValidationMode.Fast };
    }
}

public sealed class NullableTextModel
{
    public string? Value { get; init; }
}

public sealed record NonNullableTextModel(string Value);

public sealed class NumberModel
{
    public int Value { get; init; }
}

public sealed class NullableDecimalModel
{
    public decimal? Value { get; init; }
}

public sealed class NullableByteModel
{
    public byte? Byte { get; init; }

    public sbyte? SByte { get; init; }
}

public sealed class CompareModel
{
    public int Left { get; init; }

    public int Right { get; init; }
}

public enum ConditionalMode
{
    None,
    Name,
    Age
}

public enum SampleState
{
    None = 0,
    Started = 1,
    Done = 2
}

public sealed class ConditionalModel
{
    public bool ValidateName { get; init; }

    public ConditionalMode Mode { get; init; }

    public bool RequireAdult { get; init; }

    public string? Name { get; init; }

    public int Age { get; init; }
}

public sealed class EnumValidationModel
{
    public SampleState State { get; init; }

    public string? StateName { get; init; }

    public int StateValue { get; init; }
}

public sealed class CollectionModel
{
    public List<int> Values { get; init; } = [];
}

public sealed class DictionaryModel
{
    public Dictionary<string, int>? Values { get; init; }
}

public sealed class ParentModel
{
    public List<ChildModel> Children { get; init; } = [];
}

public sealed class ChildModel
{
    public string? Name { get; init; }
}

public sealed class FailureBehaviorModel
{
    public string? Name { get; init; }

    public string? Description { get; init; }
}

public sealed class LoopModel
{
    public List<int> Values { get; init; } = [];

    public string? Name { get; init; }

    public bool RunWhile { get; init; }

    public bool RunDo { get; init; }
}

public sealed class CustomerModel
{
    public AddressModel? Address { get; init; }
}

public sealed class AttributeCustomerModel
{
    [UseValidator<AddressPropertyValidator>]
    public AddressModel? Address { get; init; }
}

public sealed class AddressModel
{
    public string? Line1 { get; init; }

    public string? Zip { get; init; }
}

[ValiantValidator]
public sealed partial class NotNullValidator : IValidatorDefinition<NullableTextModel>
{
    public static void Configure(ValidationContext<NullableTextModel> context)
    {
        context.Property(x => x.Value).NotNull();
    }
}

[ValiantValidator]
public sealed partial class NullValidator : IValidatorDefinition<NullableTextModel>
{
    public static void Configure(ValidationContext<NullableTextModel> context)
    {
        context.Property(x => x.Value).Null();
    }
}

[ValiantValidator]
public sealed partial class RequiredValidator : IValidatorDefinition<NullableTextModel>
{
    public static void Configure(ValidationContext<NullableTextModel> context)
    {
        context.Property(x => x.Value).Required();
    }
}

[ValiantValidator]
public sealed partial class NotEmptyValidator : IValidatorDefinition<NullableTextModel>
{
    public static void Configure(ValidationContext<NullableTextModel> context)
    {
        context.Property(x => x.Value).NotEmpty();
    }
}

[ValiantValidator]
public sealed partial class EmptyValidator : IValidatorDefinition<NullableTextModel>
{
    public static void Configure(ValidationContext<NullableTextModel> context)
    {
        context.Property(x => x.Value).Empty();
    }
}

[ValiantValidator]
public sealed partial class LengthBetweenValidator : IValidatorDefinition<NullableTextModel>
{
    public static void Configure(ValidationContext<NullableTextModel> context)
    {
        context.Property(x => x.Value).LengthBetween(2, 3);
    }
}

[ValiantValidator]
public sealed partial class MinLengthValidator : IValidatorDefinition<NullableTextModel>
{
    public static void Configure(ValidationContext<NullableTextModel> context)
    {
        context.Property(x => x.Value).MinLength(2);
    }
}

[ValiantValidator]
public sealed partial class MaxLengthValidator : IValidatorDefinition<NullableTextModel>
{
    public static void Configure(ValidationContext<NullableTextModel> context)
    {
        context.Property(x => x.Value).MaxLength(3);
    }
}

[ValiantValidator]
public sealed partial class NonNullableStringMaxLengthValidator : IValidatorDefinition<NonNullableTextModel>
{
    public static void Configure(ValidationContext<NonNullableTextModel> context)
    {
        context.Property(x => x.Value).MaxLength(3);
    }
}

[ValiantValidator]
public sealed partial class GreaterThanValidator : IValidatorDefinition<NumberModel>
{
    public static void Configure(ValidationContext<NumberModel> context)
    {
        context.Property(x => x.Value).GreaterThan(10);
    }
}

[ValiantValidator]
public sealed partial class NullableDecimalGreaterThanValidator : IValidatorDefinition<NullableDecimalModel>
{
    public static void Configure(ValidationContext<NullableDecimalModel> context)
    {
        context.Property(x => x.Value).GreaterThan(0m);
    }
}

[ValiantValidator]
public sealed partial class NullableByteGreaterThanValidator : IValidatorDefinition<NullableByteModel>
{
    public static void Configure(ValidationContext<NullableByteModel> context)
    {
        context.Property(x => x.Byte).GreaterThan((byte)0);
        context.Property(x => x.SByte).GreaterThan((sbyte)0);
    }
}

[ValiantValidator]
public sealed partial class GreaterThanOrEqualToValidator : IValidatorDefinition<NumberModel>
{
    public static void Configure(ValidationContext<NumberModel> context)
    {
        context.Property(x => x.Value).GreaterThanOrEqualTo(10);
    }
}

[ValiantValidator]
public sealed partial class LessThanValidator : IValidatorDefinition<NumberModel>
{
    public static void Configure(ValidationContext<NumberModel> context)
    {
        context.Property(x => x.Value).LessThan(10);
    }
}

[ValiantValidator]
public sealed partial class LessThanOrEqualToValidator : IValidatorDefinition<NumberModel>
{
    public static void Configure(ValidationContext<NumberModel> context)
    {
        context.Property(x => x.Value).LessThanOrEqualTo(10);
    }
}

[ValiantValidator]
public sealed partial class EqualValidator : IValidatorDefinition<NumberModel>
{
    public static void Configure(ValidationContext<NumberModel> context)
    {
        context.Property(x => x.Value).Equal(10);
    }
}

[ValiantValidator]
public sealed partial class NotEqualValidator : IValidatorDefinition<NumberModel>
{
    public static void Configure(ValidationContext<NumberModel> context)
    {
        context.Property(x => x.Value).NotEqual(10);
    }
}

[ValiantValidator]
public sealed partial class InclusiveBetweenValidator : IValidatorDefinition<NumberModel>
{
    public static void Configure(ValidationContext<NumberModel> context)
    {
        context.Property(x => x.Value).InclusiveBetween(5, 10);
    }
}

[ValiantValidator]
public sealed partial class ExclusiveBetweenValidator : IValidatorDefinition<NumberModel>
{
    public static void Configure(ValidationContext<NumberModel> context)
    {
        context.Property(x => x.Value).ExclusiveBetween(5, 10);
    }
}

[ValiantValidator]
public sealed partial class SatisfiesValidator : IValidatorDefinition<NullableTextModel>
{
    public static void Configure(ValidationContext<NullableTextModel> context)
    {
        context.Property(x => x.Value).Satisfies(static value => value is null || value != "!");
    }
}

[ValiantValidator]
public sealed partial class SatisfiesWithModelValidator : IValidatorDefinition<CompareModel>
{
    public static void Configure(ValidationContext<CompareModel> context)
    {
        context.Property(x => x.Left).Satisfies(static (model, left) => left == model.Right);
    }
}

[ValiantValidator]
public sealed partial class SatisfiesAsyncValidator : IValidatorDefinition<NumberModel>
{
    public static void Configure(ValidationContext<NumberModel> context)
    {
        context.Property(x => x.Value).SatisfiesAsync(static (value, _) => new ValueTask<bool>(value != 13));
    }
}

[ValiantValidator]
public sealed partial class SatisfiesAsyncWithModelValidator : IValidatorDefinition<CompareModel>
{
    public static void Configure(ValidationContext<CompareModel> context)
    {
        context.Property(x => x.Left).SatisfiesAsync(static (model, left, _) => new ValueTask<bool>(left == model.Right));
    }
}

[ValiantValidator]
public sealed partial class MetadataValidator : IValidatorDefinition<NullableTextModel>
{
    public static void Configure(ValidationContext<NullableTextModel> context)
    {
        context.Property(x => x.Value).Required(code: "ValueRequired", message: "Custom message.");
    }
}

[ValiantValidator]
public sealed partial class WarningSeverityValidator : IValidatorDefinition<NullableTextModel>
{
    public static void Configure(ValidationContext<NullableTextModel> context)
    {
        context.Property(x => x.Value).Required(severity: ValiantSeverity.Warning);
    }
}

[ValiantValidator]
public sealed partial class IfElseConditionalValidator : IValidatorDefinition<ConditionalModel>
{
    public static void Configure(ValidationContext<ConditionalModel> context)
    {
        if (context.Instance.ValidateName)
        {
            context.Property(x => x.Name).Required();
        }
        else
        {
            context.Property(x => x.Age).GreaterThanOrEqualTo(18);
        }
    }
}

[ValiantValidator]
public sealed partial class SwitchConditionalValidator : IValidatorDefinition<ConditionalModel>
{
    public static void Configure(ValidationContext<ConditionalModel> context)
    {
        switch (context.Instance.Mode)
        {
            case ConditionalMode.Name:
                context.Property(x => x.Name).Required();
                break;
            case ConditionalMode.Age when context.Instance.RequireAdult:
                context.Property(x => x.Age).GreaterThanOrEqualTo(18);
                break;
            default:
                break;
        }
    }
}

[ValiantValidator]
public sealed partial class ConditionalWhenValidator : IValidatorDefinition<ConditionalModel>
{
    public static void Configure(ValidationContext<ConditionalModel> context)
    {
        context.Property(x => x.Age)
            .GreaterThanOrEqualTo(18)
            .When(request => request.RequireAdult);
    }
}

[ValiantValidator]
public sealed partial class RequiredNameValidator : IValidatorDefinition<ConditionalModel>
{
    public static void Configure(ValidationContext<ConditionalModel> context)
    {
        context.Property(x => x.Name).Required();
    }
}

[ValiantValidator]
public sealed partial class CompositeConditionalValidator : IValidatorDefinition<ConditionalModel>
{
    private readonly RequiredNameValidator _requiredNameValidator;

    public CompositeConditionalValidator(RequiredNameValidator requiredNameValidator)
    {
        _requiredNameValidator = requiredNameValidator;
    }

    public void Configure(ValidationContext<ConditionalModel> context)
    {
        context.UseValidator(_requiredNameValidator);
    }
}

[ValiantValidator]
public sealed partial class AsyncRequiredNameValidator : IValidatorDefinition<ConditionalModel>
{
    public static void Configure(ValidationContext<ConditionalModel> context)
    {
        context.Property(x => x.Name).SatisfiesAsync(static (name, _) => new ValueTask<bool>(!string.IsNullOrWhiteSpace(name)));
    }
}

[ValiantValidator]
public sealed partial class CompositeAsyncConditionalValidator : IValidatorDefinition<ConditionalModel>
{
    private readonly AsyncRequiredNameValidator _asyncRequiredNameValidator;

    public CompositeAsyncConditionalValidator(AsyncRequiredNameValidator asyncRequiredNameValidator)
    {
        _asyncRequiredNameValidator = asyncRequiredNameValidator;
    }

    public void Configure(ValidationContext<ConditionalModel> context)
    {
        context.UseValidator(_asyncRequiredNameValidator);
    }
}

[ValiantValidator]
public sealed partial class DefinedEnumValidator : IValidatorDefinition<EnumValidationModel>
{
    public static void Configure(ValidationContext<EnumValidationModel> context)
    {
        context.Property(x => x.State).DefinedEnum();
    }
}

[ValiantValidator]
public sealed partial class EnumNameValidator : IValidatorDefinition<EnumValidationModel>
{
    public static void Configure(ValidationContext<EnumValidationModel> context)
    {
        context.Property(x => x.StateName).EnumName<SampleState>();
    }
}

[ValiantValidator]
public sealed partial class EnumValueValidator : IValidatorDefinition<EnumValidationModel>
{
    public static void Configure(ValidationContext<EnumValidationModel> context)
    {
        context.Property(x => x.StateValue).EnumValue<SampleState>();
    }
}

[ValiantValidator]
public sealed partial class NotEmptyCollectionValidator : IValidatorDefinition<CollectionModel>
{
    public static void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(x => x.Values).NotEmpty();
    }
}

[ValiantValidator]
public sealed partial class CountCollectionValidator : IValidatorDefinition<CollectionModel>
{
    public static void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(x => x.Values).Count(3);
    }
}

[ValiantValidator]
public sealed partial class CountBetweenCollectionValidator : IValidatorDefinition<CollectionModel>
{
    public static void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(x => x.Values).CountBetween(2, 3);
    }
}

[ValiantValidator]
public sealed partial class MinCountCollectionValidator : IValidatorDefinition<CollectionModel>
{
    public static void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(x => x.Values).MinCount(2);
    }
}

[ValiantValidator]
public sealed partial class MaxCountCollectionValidator : IValidatorDefinition<CollectionModel>
{
    public static void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(x => x.Values).MaxCount(3);
    }
}

[ValiantValidator]
public sealed partial class LengthBetweenCollectionValidator : IValidatorDefinition<CollectionModel>
{
    public static void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(x => x.Values).LengthBetween(2, 3);
    }
}

[ValiantValidator]
public sealed partial class MinLengthCollectionValidator : IValidatorDefinition<CollectionModel>
{
    public static void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(x => x.Values).MinLength(2);
    }
}

[ValiantValidator]
public sealed partial class MaxLengthCollectionValidator : IValidatorDefinition<CollectionModel>
{
    public static void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(x => x.Values).MaxLength(3);
    }
}

[ValiantValidator]
public sealed partial class ContainsCollectionValidator : IValidatorDefinition<CollectionModel>
{
    public static void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(x => x.Values).Contains(3);
    }
}

[ValiantValidator]
public sealed partial class ExcludesCollectionValidator : IValidatorDefinition<CollectionModel>
{
    public static void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(x => x.Values).Excludes(99);
    }
}

[ValiantValidator]
public sealed partial class AllCollectionValidator : IValidatorDefinition<CollectionModel>
{
    public static void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(x => x.Values).All(value => value > 0);
    }
}

[ValiantValidator]
public sealed partial class AnyCollectionValidator : IValidatorDefinition<CollectionModel>
{
    public static void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(x => x.Values).Any(value => value == 42);
    }
}

[ValiantValidator]
public sealed partial class NoneCollectionValidator : IValidatorDefinition<CollectionModel>
{
    public static void Configure(ValidationContext<CollectionModel> context)
    {
        context.Property(x => x.Values).None(value => value < 0);
    }
}

[ValiantValidator]
public sealed partial class ChildCollectionValidator : IValidatorDefinition<ParentModel>
{
    public static void Configure(ValidationContext<ParentModel> context)
    {
        context.Property(x => x.Children).ForEach(child =>
        {
            child.Property(x => x.Name).Required();
        });
    }
}

[ValiantValidator]
public sealed partial class ChildCollectionWithCollectionParameterValidator : IValidatorDefinition<ParentModel>
{
    public static void Configure(ValidationContext<ParentModel> context)
    {
        context.Property(x => x.Children).ForEach((child, children) =>
        {
            if (children.Count > 0)
            {
                child.Property(x => x.Name).Required(code: "ChildName");
            }
        });
    }
}

[ValiantValidator]
public sealed partial class GlobalFailureBehaviorValidator : IValidatorDefinition<FailureBehaviorModel>
{
    public static void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.Property(x => x.Name).Required();
        context.Property(x => x.Description).Required();
    }
}

[ValiantValidator]
public sealed partial class ValidatorFailFastBehaviorValidator : IValidatorDefinition<FailureBehaviorModel>
{
    public static void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.FailFast();
        context.Property(x => x.Name).Required();
        context.Property(x => x.Description).Required();
    }
}

[ValiantValidator]
public sealed partial class ValidatorValidateAllBehaviorValidator : IValidatorDefinition<FailureBehaviorModel>
{
    public static void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.ValidateAll();
        context.Property(x => x.Name).Required();
        context.Property(x => x.Description).Required();
    }
}

[ValiantValidator]
public sealed partial class PropertyChainFailFastBehaviorValidator : IValidatorDefinition<FailureBehaviorModel>
{
    public static void Configure(ValidationContext<FailureBehaviorModel> context)
    {
        context.Property(x => x.Name)
            .FailFast()
            .Required()
            .Satisfies(static value => value!.Length > 0);

        context.Property(x => x.Description).Required();
    }
}

[ValiantValidator]
public sealed partial class AddressValidator : IValidatorDefinition<AddressModel>
{
    public static void Configure(ValidationContext<AddressModel> context)
    {
        context.Property(x => x.Line1).Required();
        context.Property(x => x.Zip).Required();
    }
}

[ValiantValidator]
public sealed class AddressPropertyValidator : PropertyValidatorDefinition<AddressModel>
{
    public override ValidationResult Validate(AddressModel value)
    {
        var failures = new List<ValidationFailure>();
        if (string.IsNullOrWhiteSpace(value.Line1))
        {
            failures.Add(new ValidationFailure(nameof(AddressModel.Line1), "Required", "'Line1' is required."));
        }

        if (string.IsNullOrWhiteSpace(value.Zip))
        {
            failures.Add(new ValidationFailure(nameof(AddressModel.Zip), "Required", "'Zip' is required."));
        }

        return failures.Count == 0 ? ValidationResult.Valid : new ValidationResult(failures);
    }
}

public sealed class AsyncOnlyAddressPropertyValidator : PropertyValidatorDefinition<AddressModel>
{
    public override ValueTask<ValidationResult> ValidateAsync(AddressModel value, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var failures = new List<ValidationFailure>();
        if (string.IsNullOrWhiteSpace(value.Line1))
        {
            failures.Add(new ValidationFailure(nameof(AddressModel.Line1), "Required", "'Line1' is required."));
        }

        if (string.IsNullOrWhiteSpace(value.Zip))
        {
            failures.Add(new ValidationFailure(nameof(AddressModel.Zip), "Required", "'Zip' is required."));
        }

        return new ValueTask<ValidationResult>(failures.Count == 0 ? ValidationResult.Valid : new ValidationResult(failures));
    }
}

[ValiantValidator]
public sealed partial class CustomerValidator : IValidatorDefinition<CustomerModel>
{
    private readonly AddressPropertyValidator _addressValidator;

    public CustomerValidator(AddressPropertyValidator addressValidator)
    {
        _addressValidator = addressValidator;
    }

    public void Configure(ValidationContext<CustomerModel> context)
    {
        context.Property(x => x.Address).UseValidator(_addressValidator);
    }
}

[ValiantValidator]
public sealed partial class AttributeCustomerValidator : ValidatorDefinition<AttributeCustomerModel>;

[ValiantValidator]
public sealed partial class ChildPropertyValidator : IValidatorDefinition<CustomerModel>
{
    public static void Configure(ValidationContext<CustomerModel> context)
    {
        context.Property(x => x.Address!.Line1).Required();
    }
}

[ValiantValidator]
public sealed partial class ChildrenValidator : IValidatorDefinition<CustomerModel>
{
    public static void Configure(ValidationContext<CustomerModel> context)
    {
        context.Property(x => x.Address).Children((child, wholeModel) =>
        {
            child.Property(x => x.Line1).Required();
            if (wholeModel.Address is not null)
            {
                child.Property(x => x.Zip).Required();
            }
        });
    }
}

[ValiantValidator]
public sealed partial class NativeForeachItemValidator : IValidatorDefinition<ParentModel>
{
    public static void Configure(ValidationContext<ParentModel> context)
    {
        var index = 0;
        foreach (var child in context.Instance.Children)
        {
            context.Item(child, x => x.Children, index)
                .Property(x => x.Name)
                .Required(code: "NativeChildName");
            index++;
        }
    }
}

[ValiantValidator]
public sealed partial class HasKeyDictionaryValidator : IValidatorDefinition<DictionaryModel>
{
    public static void Configure(ValidationContext<DictionaryModel> context)
    {
        context.Property(x => x.Values).HasKey("id");
    }
}

[ValiantValidator]
public sealed partial class HasValueDictionaryValidator : IValidatorDefinition<DictionaryModel>
{
    public static void Configure(ValidationContext<DictionaryModel> context)
    {
        context.Property(x => x.Values).HasValue(7);
    }
}

[ValiantValidator]
public sealed partial class LoopValidator : IValidatorDefinition<LoopModel>
{
    public static void Configure(ValidationContext<LoopModel> context)
    {
        foreach (var value in context.Instance.Values)
        {
            if (value < 0)
            {
                context.Property(x => x.Name).Required(code: "ForeachName");
            }
        }

        for (var index = 0; index < context.Instance.Values.Count; index++)
        {
            if (context.Instance.Values[index] == 0)
            {
                continue;
            }

            if (context.Instance.Values[index] < 0)
            {
                context.Property(x => x.Name).Required(code: "ForName");
            }
        }

        while (context.Instance.RunWhile)
        {
            context.Property(x => x.Name).Required(code: "WhileName");
            break;
        }

        var runDo = context.Instance.RunDo;
        do
        {
            if (runDo)
            {
                context.Property(x => x.Name).Required(code: "DoName");
            }

            runDo = false;
        }
        while (runDo);
    }
}
