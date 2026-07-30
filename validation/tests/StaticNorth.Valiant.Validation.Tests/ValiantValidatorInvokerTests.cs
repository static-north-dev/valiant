using Microsoft.Extensions.DependencyInjection;
using StaticNorth.Valiant.Validation.Infrastructure;

namespace StaticNorth.Valiant.Validation.Tests;

public sealed class ValiantValidatorInvokerTests
{
    [Test]
    public async Task ResolveValidators_Returns_A_Stable_Empty_Snapshot_When_No_Validators_Are_Registered()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var invoker = new ValiantValidatorInvoker<InvokerModel>();

        var validators = invoker.ResolveValidators(provider);
        var result = await invoker.ValidateAsync(validators, new InvokerModel(), default);

        await Assert.That(validators).IsTypeOf<IValidator<InvokerModel>[]>();
        await Assert.That((IValidator<InvokerModel>[])validators).IsEmpty();
        await Assert.That(result).IsSameReferenceAs(ValidationResult.Valid);
    }

    [Test]
    public async Task ValidateAsync_Combines_All_Severities_In_Registration_Order_And_Forwards_Cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = new List<string>();
        var first = new RecordingInvokerValidator(
            "first",
            calls,
            new ValidationFailure("Name", "FirstWarning", "first", ValiantSeverity.Warning));
        var second = new RecordingInvokerValidator(
            "second",
            calls,
            new ValidationFailure("Name", "SecondError", "second"));
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<InvokerModel>>(first);
        services.AddSingleton<IValidator<InvokerModel>>(second);
        using var provider = services.BuildServiceProvider();
        var invoker = new ValiantValidatorInvoker<InvokerModel>();

        var validators = invoker.ResolveValidators(provider);
        var result = await invoker.ValidateAsync(validators, new InvokerModel(), cancellation.Token);

        await Assert.That(calls.Count).IsEqualTo(2);
        await Assert.That(calls[0]).IsEqualTo("first");
        await Assert.That(calls[1]).IsEqualTo("second");
        await Assert.That(result.Failures.Count).IsEqualTo(2);
        await Assert.That(result.Failures[0].ErrorCode).IsEqualTo("FirstWarning");
        await Assert.That(result.Failures[1].ErrorCode).IsEqualTo("SecondError");
        await Assert.That(result.Warnings).Count().IsEqualTo(1);
        await Assert.That(result.Errors).Count().IsEqualTo(1);
        await Assert.That(first.CancellationToken).IsEqualTo(cancellation.Token);
        await Assert.That(second.CancellationToken).IsEqualTo(cancellation.Token);
    }

    [Test]
    public async Task ValidateAsync_Returns_The_Only_NonEmpty_Result_Without_Rebuilding_It()
    {
        var warningResult = new ValidationResult(
        [
            new ValidationFailure("Name", "Warning", "warning", ValiantSeverity.Warning)
        ]);
        IValidator<InvokerModel>[] validators =
        [
            new FixedResultValidator(ValidationResult.Valid),
            new FixedResultValidator(warningResult),
            new FixedResultValidator(ValidationResult.Valid)
        ];
        var invoker = new ValiantValidatorInvoker<InvokerModel>();

        var result = await invoker.ValidateAsync(validators, new InvokerModel(), default);

        await Assert.That(result).IsSameReferenceAs(warningResult);
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Warnings).Count().IsEqualTo(1);
    }

    private sealed class InvokerModel;

    private sealed class RecordingInvokerValidator(
        string name,
        List<string> calls,
        ValidationFailure failure) : IValidator<InvokerModel>
    {
        public CancellationToken CancellationToken { get; private set; }

        public ValidationResult Validate(InvokerModel instance)
        {
            calls.Add(name);
            return new ValidationResult([failure]);
        }

        public ValueTask<ValidationResult> ValidateAsync(
            InvokerModel instance,
            CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            return new ValueTask<ValidationResult>(Validate(instance));
        }

        public bool IsValid(InvokerModel instance)
        {
            return Validate(instance).IsValid;
        }

        public ValueTask<bool> IsValidAsync(
            InvokerModel instance,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<bool>(IsValid(instance));
        }
    }

    private sealed class FixedResultValidator(ValidationResult result) : IValidator<InvokerModel>
    {
        public ValidationResult Validate(InvokerModel instance)
        {
            return result;
        }

        public ValueTask<ValidationResult> ValidateAsync(
            InvokerModel instance,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<ValidationResult>(result);
        }

        public bool IsValid(InvokerModel instance)
        {
            return result.IsValid;
        }

        public ValueTask<bool> IsValidAsync(
            InvokerModel instance,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<bool>(result.IsValid);
        }
    }
}
