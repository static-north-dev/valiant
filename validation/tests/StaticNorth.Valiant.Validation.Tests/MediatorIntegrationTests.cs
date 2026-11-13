using MediatR;

namespace StaticNorth.Valiant.Validation.Tests;

public sealed class MediatorIntegrationTests
{
    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    public async Task MediatR_Behavior_Runs_Validators_Before_Next(ValiantValidationMode mode)
    {
        var behavior = new StaticNorth.Valiant.Validation.MediatR.ValiantValidationBehavior<MediatRTestRequest, string>(
        [
            new MediatRTestRequestValidator(new ValiantValidationOptions { Mode = mode })
        ]);
        var nextWasCalled = false;

        var result = await behavior.Handle(
            new MediatRTestRequest("ok"),
            _ =>
            {
                nextWasCalled = true;
                return Task.FromResult("handled");
            },
            CancellationToken.None);

        await Assert.That(result).IsEqualTo("handled");
        await Assert.That(nextWasCalled).IsTrue();
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    public async Task MediatR_Behavior_Throws_When_Validation_Fails(ValiantValidationMode mode)
    {
        var behavior = new StaticNorth.Valiant.Validation.MediatR.ValiantValidationBehavior<MediatRTestRequest, string>(
        [
            new MediatRTestRequestValidator(new ValiantValidationOptions { Mode = mode })
        ]);
        var nextWasCalled = false;

        var exception = await Assert.ThrowsAsync<ValiantValidationException>(() => behavior.Handle(
            new MediatRTestRequest(""),
            _ =>
            {
                nextWasCalled = true;
                return Task.FromResult("handled");
            },
            CancellationToken.None));

        await AssertFailures(exception!);
        await Assert.That(nextWasCalled).IsFalse();
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    public async Task Mediator_Behavior_Runs_Validators_Before_Next(ValiantValidationMode mode)
    {
        var behavior = new StaticNorth.Valiant.Validation.Mediator.ValiantValidationBehavior<MediatorTestRequest, string>(
        [
            new MediatorTestRequestValidator(new ValiantValidationOptions { Mode = mode })
        ]);
        var nextWasCalled = false;

        var result = await behavior.Handle(
            new MediatorTestRequest("ok"),
            (_, _) =>
            {
                nextWasCalled = true;
                return new ValueTask<string>("handled");
            },
            CancellationToken.None);

        await Assert.That(result).IsEqualTo("handled");
        await Assert.That(nextWasCalled).IsTrue();
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    public async Task Mediator_Behavior_Throws_When_Validation_Fails(ValiantValidationMode mode)
    {
        var behavior = new StaticNorth.Valiant.Validation.Mediator.ValiantValidationBehavior<MediatorTestRequest, string>(
        [
            new MediatorTestRequestValidator(new ValiantValidationOptions { Mode = mode })
        ]);
        var nextWasCalled = false;

        var exception = await Assert.ThrowsAsync<ValiantValidationException>(async () => await behavior.Handle(
            new MediatorTestRequest(""),
            (_, _) =>
            {
                nextWasCalled = true;
                return new ValueTask<string>("handled");
            },
            CancellationToken.None));

        await AssertFailures(exception!);
        await Assert.That(nextWasCalled).IsFalse();
    }

    private static async Task AssertFailures(ValiantValidationException exception)
    {
        await Assert.That(exception.Result.Failures.Select(static failure =>
            (failure.PropertyName, failure.ErrorCode, failure.ErrorMessage, failure.Severity)).ToArray())
            .IsEquivalentTo([("Name", "Required", "Name is required.", ValiantSeverity.Error)]);
    }
}

public sealed record MediatRTestRequest(string Name) : IRequest<string>;

public sealed record MediatorTestRequest(string Name) : global::Mediator.IRequest<string>;
