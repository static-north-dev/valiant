using StaticNorth.Valiant.Validation.Tests.StatusParity;

namespace StaticNorth.Valiant.Validation.Tests;

public sealed class ValidationStatusParityTests
{
    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    [Arguments(ValiantValidationMode.Auto)]
    public async Task Diagnostic_Code_Can_Enable_A_Later_Failing_Rule(ValiantValidationMode mode)
    {
        var validator = new StatusMutatingCodeValidator(Options(mode));

        foreach (var api in ValidationApis())
        {
            var model = new StatusParityModel();
            await Assert.That(await api(validator, model)).IsFalse();
            await Assert.That((model.DiagnosticCalls, model.TailCalls)).IsEqualTo((1, 1));
        }
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    [Arguments(ValiantValidationMode.Auto)]
    public async Task Diagnostic_Code_Can_Enable_A_Later_Async_Rule(ValiantValidationMode mode)
    {
        var validator = new StatusAsyncMutatingCodeValidator(Options(mode));
        var resultModel = new StatusParityModel();
        var statusModel = new StatusParityModel();

        var result = await validator.ValidateAsync(resultModel);
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Failures.Count).IsEqualTo(2);
        await Assert.That(await validator.IsValidAsync(statusModel)).IsFalse();
        await Assert.That((resultModel.DiagnosticCalls, resultModel.TailCalls)).IsEqualTo((1, 1));
        await Assert.That((statusModel.DiagnosticCalls, statusModel.TailCalls)).IsEqualTo((1, 1));
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    [Arguments(ValiantValidationMode.Auto)]
    public async Task Diagnostic_Message_Throws_Even_When_The_Check_Would_Pass(ValiantValidationMode mode)
    {
        var validator = new StatusThrowingMessageValidator(Options(mode));

        foreach (var api in ValidationApis())
        {
            var model = new StatusParityModel();
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => api(validator, model).AsTask());
            await Assert.That(exception?.Message).IsEqualTo("message callback failed");
            await Assert.That((model.DiagnosticCalls, model.TailCalls)).IsEqualTo((1, 0));
        }
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast, ValiantFailureBehavior.ValidateAll)]
    [Arguments(ValiantValidationMode.Debuggable, ValiantFailureBehavior.ValidateAll)]
    [Arguments(ValiantValidationMode.Fast, ValiantFailureBehavior.FailFast)]
    [Arguments(ValiantValidationMode.Debuggable, ValiantFailureBehavior.FailFast)]
    public async Task Boolean_Validation_Stops_Only_When_The_Failure_Policy_Stops_Rules(
        ValiantValidationMode mode,
        ValiantFailureBehavior behavior)
    {
        var validator = new StatusThrowingRuleValidator(new ValiantValidationOptions
        {
            Mode = mode,
            FailureBehavior = behavior
        });

        foreach (var api in ValidationApis())
        {
            var model = new StatusParityModel();
            if (behavior == ValiantFailureBehavior.FailFast)
            {
                await Assert.That(await api(validator, model)).IsFalse();
                await Assert.That(model.TailCalls).IsEqualTo(0);
            }
            else
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => api(validator, model).AsTask());
                await Assert.That(exception?.Message).IsEqualTo("tail predicate failed");
                await Assert.That(model.TailCalls).IsEqualTo(1);
            }

            await Assert.That(model.DiagnosticCalls).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    public async Task Default_Message_Formatting_Throws_Before_Comparing_Values(ValiantValidationMode mode)
    {
        var validator = new StatusDefaultFormattingValidator(Options(mode));

        foreach (var api in ValidationApis())
        {
            var model = new StatusParityModel();
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => api(validator, model).AsTask());
            await Assert.That(exception?.Message).IsEqualTo("comparison bound cannot be formatted");
            await Assert.That(model.TailCalls).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    public async Task Later_Diagnostic_Message_Can_Enable_An_Earlier_Rule(ValiantValidationMode mode)
    {
        var validator = new StatusLateMessageValidator(Options(mode));

        foreach (var api in ValidationApis())
        {
            var model = new StatusParityModel();
            await Assert.That(await api(validator, model)).IsFalse();
            await Assert.That((model.DiagnosticCalls, model.TailCalls)).IsEqualTo((1, 1));
        }
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    public async Task Whole_Model_Reuse_Calls_The_Same_API_For_Status_And_Result(ValiantValidationMode mode)
    {
        var validator = new StatusReusedValidator(new StatusManualValidator(), Options(mode));
        var syncModel = new StatusParityModel();
        var asyncModel = new StatusParityModel();

        await Assert.That(validator.IsValid(syncModel)).IsFalse();
        await Assert.That(await validator.IsValidAsync(asyncModel)).IsFalse();
        await Assert.That((syncModel.ReuseMethod, syncModel.TailCalls)).IsEqualTo((nameof(IValidator<StatusParityModel>.Validate), 1));
        await Assert.That((asyncModel.ReuseMethod, asyncModel.TailCalls)).IsEqualTo((nameof(IValidator<StatusParityModel>.ValidateAsync), 1));
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast, UnknownSeverityScope.Property)]
    [Arguments(ValiantValidationMode.Debuggable, UnknownSeverityScope.Property)]
    [Arguments(ValiantValidationMode.Fast, UnknownSeverityScope.Child)]
    [Arguments(ValiantValidationMode.Debuggable, UnknownSeverityScope.Child)]
    [Arguments(ValiantValidationMode.Fast, UnknownSeverityScope.ReusedProperty)]
    [Arguments(ValiantValidationMode.Debuggable, UnknownSeverityScope.ReusedProperty)]
    public async Task Unknown_Severity_Is_Invalid_And_Preserves_Debuggable_FailFast_Behavior(
        ValiantValidationMode mode,
        UnknownSeverityScope scope)
    {
        var validator = new StatusUnknownSeverityValidator(
            scope,
            new StatusUnknownSeverityPropertyValidator(),
            new ValiantValidationOptions { Mode = mode, FailureBehavior = ValiantFailureBehavior.FailFast });

        foreach (var api in ValidationApis())
        {
            var model = new StatusParityModel { Child = new StatusParityModel() };
            await Assert.That(await api(validator, model)).IsFalse();
            await Assert.That(model.TailCalls).IsEqualTo(1);
        }
    }

    private static ValiantValidationOptions Options(ValiantValidationMode mode) => new() { Mode = mode };

    private static Func<IValidator<StatusParityModel>, StatusParityModel, ValueTask<bool>>[] ValidationApis() =>
    [
        static (validator, model) => new(validator.Validate(model).IsValid),
        static async (validator, model) => (await validator.ValidateAsync(model)).IsValid,
        static (validator, model) => new(validator.IsValid(model)),
        static (validator, model) => validator.IsValidAsync(model)
    ];
}
