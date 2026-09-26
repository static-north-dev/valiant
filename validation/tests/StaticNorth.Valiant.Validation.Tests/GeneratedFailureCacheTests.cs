namespace StaticNorth.Valiant.Validation.Tests;

public sealed class GeneratedFailureCacheTests
{
    [Test]
    [Arguments(ValiantValidationMode.Fast)]
    [Arguments(ValiantValidationMode.Debuggable)]
    public async Task Dynamic_Messages_Are_Reevaluated_And_Retained_Results_Are_Unchanged(ValiantValidationMode mode)
    {
        var validator = new DynamicFailureMessageValidator(new ValiantValidationOptions { Mode = mode });
        var retained = validator.Validate(new NullableTextModel());
        validator.Message = "second";
        var next = validator.Validate(new NullableTextModel());

        await Assert.That(retained.Failures[0].ErrorMessage).IsEqualTo("first");
        await Assert.That(next.Failures[0].ErrorMessage).IsEqualTo("second");
        await Assert.That(ReferenceEquals(retained, next)).IsFalse();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(31)]
    public async Task Warm_Full_Result_Validation_Allocates_Zero_Bytes(int failedChecks)
    {
        var validator = new FixedFailureValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Fast });
        var model = new FixedFailureModel { FailedChecks = failedChecks };
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            GC.KeepAlive(validator.Validate(model));
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            GC.KeepAlive(validator.Validate(model));
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(allocated).IsEqualTo(0L);
        await Assert.That(model.ReadCount).IsEqualTo(10_000);
    }

    [Test]
    public async Task Every_Combination_Reruns_Checks_And_Preserves_Complete_Mode_Parity()
    {
        var fast = new FixedFailureValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Fast });
        var runtime = new FixedFailureValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Debuggable });
        var model = new FixedFailureModel();
        var retained = new ValidationResult[32];
        for (var mask = 0; mask < retained.Length; mask++)
        {
            model.FailedChecks = mask;
            var result = fast.Validate(model);
            retained[mask] = result;
            var expected = runtime.Validate(new FixedFailureModel { FailedChecks = mask });
            await Assert.That(result.Failures.SequenceEqual(expected.Failures)).IsTrue();
            await Assert.That(result.Errors.SequenceEqual(expected.Errors)).IsTrue();
            await Assert.That(result.Warnings.Count).IsEqualTo(0);
            await Assert.That(result.Infos.Count).IsEqualTo(0);
            await Assert.That(result.IsValid).IsEqualTo(mask == 0);
        }

        for (var mask = 31; mask >= 0; mask--)
        {
            model.FailedChecks = mask;
            await Assert.That(ReferenceEquals(retained[mask], fast.Validate(model))).IsTrue();
            await Assert.That(ReferenceEquals(retained[mask], await fast.ValidateAsync(new FixedFailureModel { FailedChecks = mask }))).IsTrue();
        }

        await Assert.That(model.ReadCount).IsEqualTo(320);
    }

    [Test]
    public async Task FailFast_Reruns_Only_Checks_Up_To_The_First_Error()
    {
        var validator = new FixedFailureValidator(new ValiantValidationOptions
        {
            Mode = ValiantValidationMode.Fast,
            FailureBehavior = ValiantFailureBehavior.FailFast
        });
        var model = new FixedFailureModel { FailedChecks = 31 };
        var retained = validator.Validate(model);
        await Assert.That(retained.Failures.Count).IsEqualTo(1);
        await Assert.That(retained.Failures[0].PropertyName).IsEqualTo("A");
        await Assert.That(ReferenceEquals(retained, validator.Validate(model))).IsTrue();
        await Assert.That(model.ReadCount).IsEqualTo(2);
        model.FailedChecks = 16;
        var last = validator.Validate(model);
        await Assert.That(last.Failures.Count).IsEqualTo(1);
        await Assert.That(last.Failures[0].PropertyName).IsEqualTo("E");
        await Assert.That(model.ReadCount).IsEqualTo(7);
        await Assert.That(retained.Failures[0].PropertyName).IsEqualTo("A");
    }
}
