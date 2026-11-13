namespace StaticNorth.Valiant.Validation.Tests;

public sealed class ValidationStatusFastPathTests
{
    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    [Arguments(ValiantValidationMode.Auto)]
    public async Task IsValid_Evaluates_All_Diagnostic_Arguments(ValiantValidationMode mode)
    {
        var counters = new ValidationStatusCounters();
        var validator = new DiagnosticStatusValidator(counters, new ValiantValidationOptions { Mode = mode });

        await Assert.That(validator.IsValid(new ValidationStatusModel())).IsFalse();
        await Assert.That(await validator.IsValidAsync(new ValidationStatusModel())).IsFalse();
        await Assert.That((counters.CodeCalls, counters.MessageCalls, counters.SeverityCalls)).IsEqualTo((2, 2, 2));
    }

    [Test]
    public async Task Fast_IsValid_Preserves_Warning_Chain_FailFast_And_Unknown_Severity()
    {
        var warningCounters = new ValidationStatusCounters();
        var warningValidator = new WarningChainStatusValidator(
            warningCounters,
            new ValiantValidationOptions { Mode = ValiantValidationMode.Fast });
        var unknownValidator = new UnknownSeverityStatusValidator(
            new ValiantValidationOptions { Mode = ValiantValidationMode.Fast });

        var warningOnlyIsValid = warningValidator.IsValid(new ValidationStatusModel());
        var unknownSeverityIsValid = unknownValidator.IsValid(new ValidationStatusModel());

        await Assert.That(warningOnlyIsValid).IsTrue();
        await Assert.That(warningCounters.PredicateCalls).IsEqualTo(0);
        await Assert.That(unknownSeverityIsValid).IsFalse();
    }

    [Test]
    public async Task Status_Composition_Uses_Materialized_Model_And_Property_Results()
    {
        var reusedValidator = new StatusReuseProbeValidator();
        var modelValidator = new ReusingStatusValidator(
            reusedValidator,
            new ValiantValidationOptions { Mode = ValiantValidationMode.Fast });
        var propertyCounters = new ValidationStatusCounters();
        var propertyValidator = new WarningPropertyValidator();
        var propertyReuseValidator = new PropertyReuseStatusValidator(
            propertyValidator,
            propertyCounters,
            new ValiantValidationOptions { Mode = ValiantValidationMode.Fast });

        var modelIsValid = modelValidator.IsValid(new ValidationStatusModel());
        var modelIsValidAsync = await modelValidator.IsValidAsync(new ValidationStatusModel());
        var propertyIsValid = propertyReuseValidator.IsValid(
            new ValidationStatusModel { Value = "present" });

        await Assert.That(modelIsValid).IsFalse();
        await Assert.That(modelIsValidAsync).IsFalse();
        await Assert.That(reusedValidator.ValidateCalls).IsEqualTo(2);
        await Assert.That(reusedValidator.IsValidCalls).IsEqualTo(0);
        await Assert.That(reusedValidator.IsValidAsyncCalls).IsEqualTo(0);
        await Assert.That(propertyIsValid).IsTrue();
        await Assert.That(propertyValidator.ValidateCalls).IsEqualTo(1);
        await Assert.That(propertyCounters.PredicateCalls).IsEqualTo(0);
    }
}

public sealed class ValidationStatusModel
{
    public string? Value { get; init; }
}

public sealed class ValidationStatusCounters
{
    public int CodeCalls { get; private set; }

    public int MessageCalls { get; private set; }

    public int SeverityCalls { get; private set; }

    public int PredicateCalls { get; private set; }

    public string Code()
    {
        CodeCalls++;
        return "StatusCode";
    }

    public string Message()
    {
        MessageCalls++;
        return "Status message.";
    }

    public ValiantSeverity ErrorSeverity()
    {
        SeverityCalls++;
        return ValiantSeverity.Error;
    }

    public bool Predicate(string? value)
    {
        PredicateCalls++;
        return value is not null;
    }
}

[ValiantValidator]
public sealed partial class DiagnosticStatusValidator(ValidationStatusCounters counters)
    : ValidatorDefinition<ValidationStatusModel>
{
    private readonly ValidationStatusCounters _counters = counters;

    public override void Configure(ValidationContext<ValidationStatusModel> context)
    {
        context.Property(model => model.Value).Required(
            code: _counters.Code(),
            message: _counters.Message(),
            severity: _counters.ErrorSeverity());
    }
}

[ValiantValidator]
public sealed partial class WarningChainStatusValidator(ValidationStatusCounters counters)
    : ValidatorDefinition<ValidationStatusModel>
{
    private readonly ValidationStatusCounters _counters = counters;

    public override void Configure(ValidationContext<ValidationStatusModel> context)
    {
        context.Property(model => model.Value)
            .FailFast()
            .Required(severity: ValiantSeverity.Warning)
            .Satisfies(value => _counters.Predicate(value));
    }
}

[ValiantValidator]
public sealed partial class UnknownSeverityStatusValidator
    : ValidatorDefinition<ValidationStatusModel>
{
    public override void Configure(ValidationContext<ValidationStatusModel> context)
    {
        context.Property(model => model.Value).Required(severity: (ValiantSeverity)99);
    }
}

[ValiantValidator]
public sealed partial class ReusingStatusValidator(StatusReuseProbeValidator reusedValidator)
    : ValidatorDefinition<ValidationStatusModel>
{
    private readonly StatusReuseProbeValidator _reusedValidator = reusedValidator;

    public override void Configure(ValidationContext<ValidationStatusModel> context)
    {
        context.UseValidator(_reusedValidator);
    }
}

[ValiantValidator]
public sealed partial class PropertyReuseStatusValidator(
    WarningPropertyValidator propertyValidator,
    ValidationStatusCounters counters) : ValidatorDefinition<ValidationStatusModel>
{
    private readonly WarningPropertyValidator _propertyValidator = propertyValidator;
    private readonly ValidationStatusCounters _counters = counters;

    public override void Configure(ValidationContext<ValidationStatusModel> context)
    {
        context.Property(model => model.Value)
            .FailFast()
            .UseValidator(_propertyValidator)
            .Satisfies(value => _counters.Predicate(value));
    }
}

public sealed class StatusReuseProbeValidator : IValidator<ValidationStatusModel>
{
    public int ValidateCalls { get; private set; }

    public int IsValidCalls { get; private set; }

    public int IsValidAsyncCalls { get; private set; }

    public ValidationResult Validate(ValidationStatusModel instance)
    {
        ValidateCalls++;
        return new ValidationResult(
        [
            new ValidationFailure("", "Reused", "reused")
        ]);
    }

    public ValueTask<ValidationResult> ValidateAsync(
        ValidationStatusModel instance,
        CancellationToken cancellationToken = default)
    {
        return new ValueTask<ValidationResult>(Validate(instance));
    }

    public bool IsValid(ValidationStatusModel instance)
    {
        IsValidCalls++;
        return false;
    }

    public ValueTask<bool> IsValidAsync(
        ValidationStatusModel instance,
        CancellationToken cancellationToken = default)
    {
        IsValidAsyncCalls++;
        return new ValueTask<bool>(false);
    }
}

public sealed class WarningPropertyValidator : IPropertyValidator<string>
{
    public int ValidateCalls { get; private set; }

    public ValidationResult Validate(string value)
    {
        ValidateCalls++;
        return new ValidationResult(
        [
            new ValidationFailure("", "Warning", "warning", ValiantSeverity.Warning)
        ]);
    }

    public ValueTask<ValidationResult> ValidateAsync(
        string value,
        CancellationToken cancellationToken = default)
    {
        return new ValueTask<ValidationResult>(Validate(value));
    }
}
