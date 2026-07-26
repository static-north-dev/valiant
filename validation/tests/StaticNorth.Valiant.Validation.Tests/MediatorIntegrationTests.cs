using MediatR;

namespace StaticNorth.Valiant.Validation.Tests;

public sealed class MediatorIntegrationTests
{
    [Test]
    public async Task MediatR_Behavior_Runs_Validators_Before_Next()
    {
        var behavior = new StaticNorth.Valiant.Validation.MediatR.ValiantValidationBehavior<MediatRTestRequest, string>(
        [
            new MediatRTestRequestValidator()
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
    public async Task MediatR_Behavior_Throws_When_Validation_Fails()
    {
        var behavior = new StaticNorth.Valiant.Validation.MediatR.ValiantValidationBehavior<MediatRTestRequest, string>(
        [
            new MediatRTestRequestValidator()
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

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(nextWasCalled).IsFalse();
    }

    [Test]
    public async Task Mediator_Behavior_Runs_Validators_Before_Next()
    {
        var behavior = new StaticNorth.Valiant.Validation.Mediator.ValiantValidationBehavior<MediatorTestRequest, string>(
        [
            new MediatorTestRequestValidator()
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
    public async Task Mediator_Behavior_Throws_When_Validation_Fails()
    {
        var behavior = new StaticNorth.Valiant.Validation.Mediator.ValiantValidationBehavior<MediatorTestRequest, string>(
        [
            new MediatorTestRequestValidator()
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

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(nextWasCalled).IsFalse();
    }
}

public sealed record MediatRTestRequest(string Name) : IRequest<string>;

public sealed class MediatRTestRequestValidator : IValidator<MediatRTestRequest>
{
    public ValidationResult Validate(MediatRTestRequest instance)
    {
        return string.IsNullOrWhiteSpace(instance.Name)
            ? new ValidationResult([new ValidationFailure(nameof(instance.Name), "Required", "Name is required.")])
            : ValidationResult.Valid;
    }

    public ValueTask<ValidationResult> ValidateAsync(MediatRTestRequest instance, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return new ValueTask<ValidationResult>(Validate(instance));
    }

    public bool IsValid(MediatRTestRequest instance)
    {
        return Validate(instance).IsValid;
    }

    public ValueTask<bool> IsValidAsync(MediatRTestRequest instance, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return new ValueTask<bool>(IsValid(instance));
    }
}

public sealed record MediatorTestRequest(string Name) : global::Mediator.IRequest<string>;

public sealed class MediatorTestRequestValidator : IValidator<MediatorTestRequest>
{
    public ValidationResult Validate(MediatorTestRequest instance)
    {
        return string.IsNullOrWhiteSpace(instance.Name)
            ? new ValidationResult([new ValidationFailure(nameof(instance.Name), "Required", "Name is required.")])
            : ValidationResult.Valid;
    }

    public ValueTask<ValidationResult> ValidateAsync(MediatorTestRequest instance, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return new ValueTask<ValidationResult>(Validate(instance));
    }

    public bool IsValid(MediatorTestRequest instance)
    {
        return Validate(instance).IsValid;
    }

    public ValueTask<bool> IsValidAsync(MediatorTestRequest instance, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return new ValueTask<bool>(IsValid(instance));
    }
}
