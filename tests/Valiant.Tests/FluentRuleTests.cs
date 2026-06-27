namespace Valiant.Tests;

public sealed class FluentRuleTests
{
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
    public async Task GreaterThan_Fails_When_Value_Is_Equal()
    {
        var result = new GreaterThanValidator().Validate(new NumberModel { Value = 10 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("GreaterThan");
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
}

public sealed class NullableTextModel
{
    public string? Value { get; init; }
}

public sealed class NumberModel
{
    public int Value { get; init; }
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

public sealed class ConditionalModel
{
    public bool ValidateName { get; init; }

    public ConditionalMode Mode { get; init; }

    public bool RequireAdult { get; init; }

    public string? Name { get; init; }

    public int Age { get; init; }
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
public sealed partial class GreaterThanValidator : IValidatorDefinition<NumberModel>
{
    public static void Configure(ValidationContext<NumberModel> context)
    {
        context.Property(x => x.Value).GreaterThan(10);
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
