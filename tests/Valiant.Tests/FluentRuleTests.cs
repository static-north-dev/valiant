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
    public async Task NotEmpty_Fails_For_Empty_String()
    {
        var result = new NotEmptyValidator().Validate(new NullableTextModel { Value = "" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("NotEmpty");
    }

    [Test]
    public async Task Empty_Fails_For_Non_Empty_String()
    {
        var result = new EmptyValidator().Validate(new NullableTextModel { Value = "x" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Empty");
    }

    [Test]
    public async Task Length_Fails_When_String_Is_Outside_Range()
    {
        var result = new LengthValidator().Validate(new NullableTextModel { Value = "abcd" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Length");
    }

    [Test]
    public async Task MinimumLength_Fails_When_String_Is_Too_Short()
    {
        var result = new MinimumLengthValidator().Validate(new NullableTextModel { Value = "a" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("MinimumLength");
    }

    [Test]
    public async Task MaximumLength_Fails_When_String_Is_Too_Long()
    {
        var result = new MaximumLengthValidator().Validate(new NullableTextModel { Value = "abcd" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("MaximumLength");
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
    public async Task Must_Fails_When_Predicate_Returns_False()
    {
        var result = new MustValidator().Validate(new NullableTextModel { Value = "!" });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Must");
    }

    [Test]
    public async Task Must_With_Model_And_Property_Fails_When_Predicate_Returns_False()
    {
        var result = new MustWithModelValidator().Validate(new CompareModel { Left = 1, Right = 2 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Must");
    }

    [Test]
    public async Task MustAsync_Fails_When_Predicate_Returns_False()
    {
        var result = await new MustAsyncValidator().ValidateAsync(new NumberModel { Value = 13 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("MustAsync");
    }

    [Test]
    public async Task MustAsync_With_Model_And_Property_Fails_When_Predicate_Returns_False()
    {
        var result = await new MustAsyncWithModelValidator().ValidateAsync(new CompareModel { Left = 2, Right = 1 });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("MustAsync");
    }

    [Test]
    public async Task WithMessage_Replaces_Default_Message()
    {
        var result = new WithMessageValidator().Validate(new NullableTextModel { Value = "" });

        await Assert.That(result.Errors[0].ErrorMessage).IsEqualTo("Custom message.");
    }

    [Test]
    public async Task IsValid_Returns_True_For_Sync_Only_Valid_Model()
    {
        var isValid = new NotEmptyValidator().IsValid(new NullableTextModel { Value = "ok" });

        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task IsValidAsync_Runs_Sync_And_Async_Rules()
    {
        var isValid = await new MustAsyncValidator().IsValidAsync(new NumberModel { Value = 12 });

        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task Validate_Throws_For_Validator_With_Async_Rules()
    {
        var validator = new MustAsyncValidator();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.FromResult(validator.Validate(new NumberModel { Value = 12 })));
    }

    [Test]
    public async Task If_Else_Condition_Runs_Only_Selected_Rules()
    {
        var validator = new IfElseConditionalValidator();

        var nameResult = validator.Validate(new ConditionalModel { ValidateName = true, Name = "", Age = 0 });
        var ageResult = validator.Validate(new ConditionalModel { ValidateName = false, Name = "", Age = 17 });
        var skippedAgeResult = validator.Validate(new ConditionalModel { ValidateName = true, Name = "ok", Age = 17 });

        await Assert.That(nameResult.Errors[0].ErrorCode).IsEqualTo("NotEmpty");
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

        await Assert.That(nameResult.Errors[0].ErrorCode).IsEqualTo("NotEmpty");
        await Assert.That(ageResult.Errors[0].ErrorCode).IsEqualTo("GreaterThanOrEqualTo");
        await Assert.That(skippedWhenResult.IsValid).IsTrue();
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
    public static ValidationContext<NullableTextModel> Configure(ValidationContext<NullableTextModel> context)
    {
        context.RuleFor(x => x.Value).NotNull();
        return context;
    }
}

[ValiantValidator]
public sealed partial class NullValidator : IValidatorDefinition<NullableTextModel>
{
    public static ValidationContext<NullableTextModel> Configure(ValidationContext<NullableTextModel> context)
    {
        context.RuleFor(x => x.Value).Null();
        return context;
    }
}

[ValiantValidator]
public sealed partial class NotEmptyValidator : IValidatorDefinition<NullableTextModel>
{
    public static ValidationContext<NullableTextModel> Configure(ValidationContext<NullableTextModel> context)
    {
        context.RuleFor(x => x.Value).NotEmpty();
        return context;
    }
}

[ValiantValidator]
public sealed partial class EmptyValidator : IValidatorDefinition<NullableTextModel>
{
    public static ValidationContext<NullableTextModel> Configure(ValidationContext<NullableTextModel> context)
    {
        context.RuleFor(x => x.Value).Empty();
        return context;
    }
}

[ValiantValidator]
public sealed partial class LengthValidator : IValidatorDefinition<NullableTextModel>
{
    public static ValidationContext<NullableTextModel> Configure(ValidationContext<NullableTextModel> context)
    {
        context.RuleFor(x => x.Value).Length(2, 3);
        return context;
    }
}

[ValiantValidator]
public sealed partial class MinimumLengthValidator : IValidatorDefinition<NullableTextModel>
{
    public static ValidationContext<NullableTextModel> Configure(ValidationContext<NullableTextModel> context)
    {
        context.RuleFor(x => x.Value).MinimumLength(2);
        return context;
    }
}

[ValiantValidator]
public sealed partial class MaximumLengthValidator : IValidatorDefinition<NullableTextModel>
{
    public static ValidationContext<NullableTextModel> Configure(ValidationContext<NullableTextModel> context)
    {
        context.RuleFor(x => x.Value).MaximumLength(3);
        return context;
    }
}

[ValiantValidator]
public sealed partial class GreaterThanValidator : IValidatorDefinition<NumberModel>
{
    public static ValidationContext<NumberModel> Configure(ValidationContext<NumberModel> context)
    {
        context.RuleFor(x => x.Value).GreaterThan(10);
        return context;
    }
}

[ValiantValidator]
public sealed partial class GreaterThanOrEqualToValidator : IValidatorDefinition<NumberModel>
{
    public static ValidationContext<NumberModel> Configure(ValidationContext<NumberModel> context)
    {
        context.RuleFor(x => x.Value).GreaterThanOrEqualTo(10);
        return context;
    }
}

[ValiantValidator]
public sealed partial class LessThanValidator : IValidatorDefinition<NumberModel>
{
    public static ValidationContext<NumberModel> Configure(ValidationContext<NumberModel> context)
    {
        context.RuleFor(x => x.Value).LessThan(10);
        return context;
    }
}

[ValiantValidator]
public sealed partial class LessThanOrEqualToValidator : IValidatorDefinition<NumberModel>
{
    public static ValidationContext<NumberModel> Configure(ValidationContext<NumberModel> context)
    {
        context.RuleFor(x => x.Value).LessThanOrEqualTo(10);
        return context;
    }
}

[ValiantValidator]
public sealed partial class EqualValidator : IValidatorDefinition<NumberModel>
{
    public static ValidationContext<NumberModel> Configure(ValidationContext<NumberModel> context)
    {
        context.RuleFor(x => x.Value).Equal(10);
        return context;
    }
}

[ValiantValidator]
public sealed partial class NotEqualValidator : IValidatorDefinition<NumberModel>
{
    public static ValidationContext<NumberModel> Configure(ValidationContext<NumberModel> context)
    {
        context.RuleFor(x => x.Value).NotEqual(10);
        return context;
    }
}

[ValiantValidator]
public sealed partial class InclusiveBetweenValidator : IValidatorDefinition<NumberModel>
{
    public static ValidationContext<NumberModel> Configure(ValidationContext<NumberModel> context)
    {
        context.RuleFor(x => x.Value).InclusiveBetween(5, 10);
        return context;
    }
}

[ValiantValidator]
public sealed partial class ExclusiveBetweenValidator : IValidatorDefinition<NumberModel>
{
    public static ValidationContext<NumberModel> Configure(ValidationContext<NumberModel> context)
    {
        context.RuleFor(x => x.Value).ExclusiveBetween(5, 10);
        return context;
    }
}

[ValiantValidator]
public sealed partial class MustValidator : IValidatorDefinition<NullableTextModel>
{
    public static ValidationContext<NullableTextModel> Configure(ValidationContext<NullableTextModel> context)
    {
        context.RuleFor(x => x.Value).Must(static value => value is null || value != "!");
        return context;
    }
}

[ValiantValidator]
public sealed partial class MustWithModelValidator : IValidatorDefinition<CompareModel>
{
    public static ValidationContext<CompareModel> Configure(ValidationContext<CompareModel> context)
    {
        context.RuleFor(x => x.Left).Must(static (model, left) => left == model.Right);
        return context;
    }
}

[ValiantValidator]
public sealed partial class MustAsyncValidator : IValidatorDefinition<NumberModel>
{
    public static ValidationContext<NumberModel> Configure(ValidationContext<NumberModel> context)
    {
        context.RuleFor(x => x.Value).MustAsync(static (value, _) => new ValueTask<bool>(value != 13));
        return context;
    }
}

[ValiantValidator]
public sealed partial class MustAsyncWithModelValidator : IValidatorDefinition<CompareModel>
{
    public static ValidationContext<CompareModel> Configure(ValidationContext<CompareModel> context)
    {
        context.RuleFor(x => x.Left).MustAsync(static (model, left, _) => new ValueTask<bool>(left == model.Right));
        return context;
    }
}

[ValiantValidator]
public sealed partial class WithMessageValidator : IValidatorDefinition<NullableTextModel>
{
    public static ValidationContext<NullableTextModel> Configure(ValidationContext<NullableTextModel> context)
    {
        context.RuleFor(x => x.Value).NotEmpty().WithMessage("Custom message.");
        return context;
    }
}

[ValiantValidator]
public sealed partial class IfElseConditionalValidator : IValidatorDefinition<ConditionalModel>
{
    public static ValidationContext<ConditionalModel> Configure(ValidationContext<ConditionalModel> context)
    {
        if (context.Instance.ValidateName)
        {
            context.RuleFor(x => x.Name).NotEmpty();
        }
        else
        {
            context.RuleFor(x => x.Age).GreaterThanOrEqualTo(18);
        }

        return context;
    }
}

[ValiantValidator]
public sealed partial class SwitchConditionalValidator : IValidatorDefinition<ConditionalModel>
{
    public static ValidationContext<ConditionalModel> Configure(ValidationContext<ConditionalModel> context)
    {
        switch (context.Instance.Mode)
        {
            case ConditionalMode.Name:
                context.RuleFor(x => x.Name).NotEmpty();
                break;
            case ConditionalMode.Age when context.Instance.RequireAdult:
                context.RuleFor(x => x.Age).GreaterThanOrEqualTo(18);
                break;
            default:
                break;
        }

        return context;
    }
}
