using Microsoft.Extensions.DependencyInjection;
using StaticNorth.Valiant.Validation.DependencyInjection;
using StaticNorth.Valiant.Validation.Tests.EdgeParity;

namespace StaticNorth.Valiant.Validation.Tests;

public sealed class SimpleFluentValidationTests
{
    [Test]
    [Arguments(ValiantValidationMode.Auto, ValiantFailureBehavior.ValidateAll)]
    [Arguments(ValiantValidationMode.Fast, ValiantFailureBehavior.ValidateAll)]
    [Arguments(ValiantValidationMode.Debuggable, ValiantFailureBehavior.ValidateAll)]
    [Arguments(ValiantValidationMode.Auto, ValiantFailureBehavior.FailFast)]
    [Arguments(ValiantValidationMode.Fast, ValiantFailureBehavior.FailFast)]
    [Arguments(ValiantValidationMode.Debuggable, ValiantFailureBehavior.FailFast)]
    public async Task DI_Resolves_Simple_Fluent_Parent_And_Child_With_Matching_Results(
        ValiantValidationMode mode, ValiantFailureBehavior behavior)
    {
        var services = new ServiceCollection();
        // Referenced sample assemblies also emit this registration class.
#pragma warning disable CS0436
        ValiantServiceCollectionExtensions.AddValiantValidators(services, options =>
        {
            options.Mode = mode;
            options.FailureBehavior = behavior;
        });
#pragma warning restore CS0436
        using var provider = services.BuildServiceProvider();
        var parent = provider.GetRequiredService<IValidator<SimpleFluentParent>>();
        var child = provider.GetRequiredService<IValidator<SimpleFluentProbe>>();
        var titleFailure = new ValidationFailure("Title", "Required", "'Title' is required.");
        var nameFailure = new ValidationFailure("Child.Name", "Required", "'Name' is required.");
        var lengthFailure = new ValidationFailure("Child.Name", "MinLength", "'Name' must be at least 3 characters.");

        await Check(() => new SimpleFluentParent("valid", null), []);
        await Check(() => new SimpleFluentParent("valid", new SimpleFluentProbe { NameValue = "valid" }), []);
        await Check(() => new SimpleFluentParent(null, new SimpleFluentProbe { NameValue = "" }),
            behavior == ValiantFailureBehavior.FailFast ? [titleFailure] : [titleFailure, nameFailure, lengthFailure]);
        await Check(() => new SimpleFluentParent("valid", new SimpleFluentProbe { NameValue = "" }),
            behavior == ValiantFailureBehavior.FailFast ? [nameFailure] : [nameFailure, lengthFailure]);

        var firstChildResult = child.Validate(new SimpleFluentProbe());
        var secondChildResult = await child.ValidateAsync(new SimpleFluentProbe());
        await Assert.That(ReferenceEquals(firstChildResult, secondChildResult)).IsEqualTo(mode != ValiantValidationMode.Debuggable);

        using var cancellation = new CancellationTokenSource();
        var cancellingChild = new SimpleFluentProbe { NameValue = "valid", Cancellation = cancellation };
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => parent.ValidateAsync(new SimpleFluentParent("valid", cancellingChild), cancellation.Token).AsTask());
        await Assert.That(exception?.CancellationToken).IsEqualTo(cancellation.Token);
        await Assert.That(string.Join(",", cancellingChild.Reads)).IsEqualTo("Name");

        async Task Check(Func<SimpleFluentParent> create, ValidationFailure[] expected)
        {
            foreach (var result in new[] { parent.Validate(create()), await parent.ValidateAsync(create()) })
            {
                await Assert.That(result.Failures.SequenceEqual(expected)).IsTrue();
                await Assert.That(result.Errors.SequenceEqual(expected)).IsTrue();
                await Assert.That(result.Warnings).IsEmpty();
                await Assert.That(result.Infos).IsEmpty();
            }

            await Assert.That(parent.IsValid(create())).IsEqualTo(expected.Length == 0);
            await Assert.That(await parent.IsValidAsync(create())).IsEqualTo(expected.Length == 0);
        }
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast, true)]
    [Arguments(ValiantValidationMode.Debuggable, false)]
    [Arguments(ValiantValidationMode.Auto, true)]
    public async Task Only_Explicit_Debuggable_Uses_The_Runtime_Path(ValiantValidationMode mode, bool usesGeneratedCache)
    {
        var validator = new SimpleFluentProbeValidator(new ValiantValidationOptions { Mode = mode });
        var first = validator.Validate(new SimpleFluentProbe());
        var second = validator.Validate(new SimpleFluentProbe());
        var asyncResult = await validator.ValidateAsync(new SimpleFluentProbe());

        await Assert.That(first.IsValid).IsFalse();
        await Assert.That(second.Failures.SequenceEqual(first.Failures)).IsTrue();
        await Assert.That(asyncResult.Failures.SequenceEqual(first.Failures)).IsTrue();
        await Assert.That(ReferenceEquals(first, second)).IsEqualTo(usesGeneratedCache);
        await Assert.That(ReferenceEquals(first, asyncResult)).IsEqualTo(usesGeneratedCache);
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast, false)]
    [Arguments(ValiantValidationMode.Debuggable, false)]
    [Arguments(ValiantValidationMode.Auto, false)]
    [Arguments(ValiantValidationMode.Fast, true)]
    [Arguments(ValiantValidationMode.Debuggable, true)]
    [Arguments(ValiantValidationMode.Auto, true)]
    public async Task Modes_Match_Ordered_Diagnostics_And_Getter_Reads(ValiantValidationMode mode, bool valid)
    {
        var validator = new SimpleFluentProbeValidator(new ValiantValidationOptions { Mode = mode });
        var debuggable = new SimpleFluentProbeValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Debuggable });
        var context = new ValidationContext<SimpleFluentProbe>(CreateModel());
        debuggable.Configure(context);
        var expected = context.ToResult();

        var syncModel = CreateModel();
        var asyncModel = CreateModel();
        var syncStatusModel = CreateModel();
        var asyncStatusModel = CreateModel();
        var actual = validator.Validate(syncModel);
        var asyncResult = await validator.ValidateAsync(asyncModel);

        await Assert.That(actual.IsValid).IsEqualTo(valid);
        await Assert.That(asyncResult.IsValid).IsEqualTo(valid);
        await Assert.That(validator.IsValid(syncStatusModel)).IsEqualTo(valid);
        await Assert.That(await validator.IsValidAsync(asyncStatusModel)).IsEqualTo(valid);
        await Assert.That(string.Join(",", expected.Failures.Select(static failure => failure.ErrorCode)))
            .IsEqualTo(valid ? "" : "Required,MinLength,InclusiveBetween");
        foreach (var result in new[] { actual, asyncResult })
        {
            await Assert.That(result.Failures.SequenceEqual(expected.Failures)).IsTrue();
            await Assert.That(result.Errors.SequenceEqual(expected.Errors)).IsTrue();
            await Assert.That(result.Warnings).IsEmpty();
            await Assert.That(result.Infos).IsEmpty();
        }

        foreach (var model in new[] { syncModel, asyncModel, syncStatusModel, asyncStatusModel })
        {
            await Assert.That(string.Join(",", model.Reads)).IsEqualTo("Name,Age");
        }

        SimpleFluentProbe CreateModel() => new() { NameValue = valid ? "valid" : "", AgeValue = valid ? null : 12 };
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    [Arguments(ValiantValidationMode.Auto)]
    public async Task Modes_Propagate_Getter_Exceptions(ValiantValidationMode mode)
    {
        var validator = new SimpleFluentProbeValidator(new ValiantValidationOptions { Mode = mode });
        foreach (var validate in ValidationApis(validator))
        {
            var model = new SimpleFluentProbe { NameValue = "", ThrowOnAge = true };
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => validate(model).AsTask());
            await Assert.That(exception?.Message).IsEqualTo("Age getter failed.");
            await Assert.That(string.Join(",", model.Reads)).IsEqualTo("Name,Age");
        }
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    [Arguments(ValiantValidationMode.Auto)]
    public async Task Modes_Stop_At_The_Same_Check_Under_FailFast(ValiantValidationMode mode)
    {
        var validator = new SimpleFluentProbeValidator(new ValiantValidationOptions
        {
            Mode = mode,
            FailureBehavior = ValiantFailureBehavior.FailFast
        });

        var result = validator.Validate(new SimpleFluentProbe { NameValue = "", ThrowOnAge = true });
        var asyncResult = await validator.ValidateAsync(new SimpleFluentProbe { NameValue = "", ThrowOnAge = true });
        await Assert.That(result.Failures.Single().ErrorCode).IsEqualTo("Required");
        await Assert.That(asyncResult.Failures.SequenceEqual(result.Failures)).IsTrue();

        foreach (var validate in ValidationApis(validator))
        {
            var model = new SimpleFluentProbe { NameValue = "", ThrowOnAge = true };
            await Assert.That(await validate(model)).IsFalse();
            await Assert.That(string.Join(",", model.Reads)).IsEqualTo("Name");
        }
    }

    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    [Arguments(ValiantValidationMode.Auto)]
    public async Task Modes_Honor_Cancellation_At_The_Same_Points(ValiantValidationMode mode)
    {
        var options = new ValiantValidationOptions { Mode = mode };
        var validator = new SimpleFluentProbeValidator(options);
        foreach (var validate in new Func<SimpleFluentProbe, CancellationToken, Task<bool>>[]
        {
            async (model, token) => (await validator.ValidateAsync(model, token)).IsValid,
            (model, token) => validator.IsValidAsync(model, token).AsTask()
        })
        {
            using var cancellation = new CancellationTokenSource();
            var model = new SimpleFluentProbe { NameValue = "ok", Cancellation = cancellation };
            var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => validate(model, cancellation.Token));
            await Assert.That(exception?.CancellationToken).IsEqualTo(cancellation.Token);
            await Assert.That(string.Join(",", model.Reads)).IsEqualTo("Name");

            var preCancelledModel = new SimpleFluentProbe();
            await Assert.ThrowsAsync<OperationCanceledException>(() => validate(preCancelledModel, cancellation.Token));
            await Assert.That(preCancelledModel.Reads).IsEmpty();

            using var failFastCancellation = new CancellationTokenSource();
            options.FailureBehavior = ValiantFailureBehavior.FailFast;
            var failFastModel = new SimpleFluentProbe { Cancellation = failFastCancellation };
            await Assert.That(await validate(failFastModel, failFastCancellation.Token)).IsFalse();
            await Assert.That(string.Join(",", failFastModel.Reads)).IsEqualTo("Name");
            options.FailureBehavior = ValiantFailureBehavior.ValidateAll;
        }
    }

    private static Func<SimpleFluentProbe, ValueTask<bool>>[] ValidationApis(SimpleFluentProbeValidator validator) =>
    [
        model => new(validator.Validate(model).IsValid),
        async model => (await validator.ValidateAsync(model)).IsValid,
        model => new(validator.IsValid(model)),
        model => validator.IsValidAsync(model)
    ];
}
