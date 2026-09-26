using System.Text.Json;
using StaticNorth.Valiant.Validation.Infrastructure;

namespace StaticNorth.Valiant.Validation.Tests;

public sealed class ValidationResultTests
{
    [Test]
    [Arguments(0, 0)]
    [Arguments(1, 96)]
    [Arguments(5, 256)]
    public async Task Accumulator_Allocates_Only_The_Owned_Result(int failureCount, int maximumBytesPerResult)
    {
        const int iterations = 1_000;
        var failure = new ValidationFailure("Name", "Required", "required");

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            GC.KeepAlive(BuildResult());
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            GC.KeepAlive(BuildResult());
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsLessThanOrEqualTo((long)maximumBytesPerResult * iterations);

        ValidationResult BuildResult()
        {
            var accumulator = new ValidationResultAccumulator(5);
            for (var index = 0; index < failureCount; index++)
            {
                accumulator.Add(failure);
            }

            return accumulator.Build();
        }
    }

    [Test]
    public async Task Constructor_Snapshots_Replaced_Input_Items()
    {
        var failures = new List<ValidationFailure>
        {
            new("Name", "Original", "original")
        };

        var result = new ValidationResult(failures);
        failures[0] = new ValidationFailure("Name", "Replacement", "replacement");

        await Assert.That(result.Failures[0].ErrorCode).IsEqualTo("Original");
        await Assert.That(result.Errors[0].ErrorCode).IsEqualTo("Original");
    }

    [Test]
    [Arguments(ValiantSeverity.Error)]
    [Arguments(ValiantSeverity.Warning)]
    [Arguments(ValiantSeverity.Info)]
    public async Task Single_Failure_Preserves_Severity_Order_And_ReadOnly_Access(ValiantSeverity severity)
    {
        var builder = new ValidationResultBuilder(5);
        builder.Add(new ValidationFailure("Name", "Required", "required", severity));
        var result = builder.Build();

        await Assert.That(result.IsValid).IsEqualTo(severity != ValiantSeverity.Error);
        await Assert.That(result.Failures.Count).IsEqualTo(1);
        await Assert.That(result.Failures.Single().ErrorCode).IsEqualTo("Required");
        await Assert.That(result.Failures[0].Severity).IsEqualTo(severity);
        await Assert.That(result.Errors.Count).IsEqualTo(severity == ValiantSeverity.Error ? 1 : 0);
        await Assert.That(result.Warnings.Count).IsEqualTo(severity == ValiantSeverity.Warning ? 1 : 0);
        await Assert.That(result.Infos.Count).IsEqualTo(severity == ValiantSeverity.Info ? 1 : 0);
        await Assert.That(result.Failures as ValidationFailure[]).IsNull();
        await Assert.That(result.Failures as IList<ValidationFailure>).IsNull();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.Run(() => result.Failures[-1]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.Run(() => result.Failures[1]));
    }

    [Test]
    [Arguments(1)]
    [Arguments(5)]
    public async Task Result_Preserves_Its_Json_Object_And_Failure_Array_Shape(int count)
    {
        var builder = new ValidationResultBuilder(count);
        for (var index = 0; index < count; index++)
        {
            builder.Add(new ValidationFailure("Name", "Required", "required"));
        }

        var json = JsonSerializer.SerializeToElement(builder.Build());

        await Assert.That(json.ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(json.GetProperty("IsValid").GetBoolean()).IsFalse();
        await Assert.That(json.GetProperty("Failures").GetArrayLength()).IsEqualTo(count);
        await Assert.That(json.GetProperty("Errors").GetArrayLength()).IsEqualTo(count);
        await Assert.That(json.GetProperty("Warnings").GetArrayLength()).IsEqualTo(0);
        await Assert.That(json.GetProperty("Infos").GetArrayLength()).IsEqualTo(0);
        await Assert.That(json.GetProperty("Errors")[0].GetProperty("ErrorCode").GetString()).IsEqualTo("Required");
    }

    [Test]
    public async Task Constructor_Partitions_Severities_In_Original_Order()
    {
        var result = new ValidationResult(
        [
            new ValidationFailure("FirstError", "FirstError", "first error"),
            new ValidationFailure("FirstWarning", "FirstWarning", "first warning", ValiantSeverity.Warning),
            new ValidationFailure("FirstInfo", "FirstInfo", "first info", ValiantSeverity.Info),
            new ValidationFailure("SecondError", "SecondError", "second error"),
            new ValidationFailure("SecondWarning", "SecondWarning", "second warning", ValiantSeverity.Warning),
            new ValidationFailure("SecondInfo", "SecondInfo", "second info", ValiantSeverity.Info)
        ]);

        await Assert.That(result.Failures).Count().IsEqualTo(6);
        await Assert.That(result.Failures[0].PropertyName).IsEqualTo("FirstError");
        await Assert.That(result.Failures[1].PropertyName).IsEqualTo("FirstWarning");
        await Assert.That(result.Failures[2].PropertyName).IsEqualTo("FirstInfo");
        await Assert.That(result.Failures[3].PropertyName).IsEqualTo("SecondError");
        await Assert.That(result.Failures[4].PropertyName).IsEqualTo("SecondWarning");
        await Assert.That(result.Failures[5].PropertyName).IsEqualTo("SecondInfo");
        await Assert.That(result.Errors).Count().IsEqualTo(2);
        await Assert.That(result.Errors[0].PropertyName).IsEqualTo("FirstError");
        await Assert.That(result.Errors[1].PropertyName).IsEqualTo("SecondError");
        await Assert.That(result.Warnings).Count().IsEqualTo(2);
        await Assert.That(result.Warnings[0].PropertyName).IsEqualTo("FirstWarning");
        await Assert.That(result.Warnings[1].PropertyName).IsEqualTo("SecondWarning");
        await Assert.That(result.Infos).Count().IsEqualTo(2);
        await Assert.That(result.Infos[0].PropertyName).IsEqualTo("FirstInfo");
        await Assert.That(result.Infos[1].PropertyName).IsEqualTo("SecondInfo");
    }

    [Test]
    public async Task Constructor_Treats_Unknown_Severity_As_Error()
    {
        var result = new ValidationResult(
        [
            new ValidationFailure("Value", "UnknownSeverity", "unknown", (ValiantSeverity)99)
        ]);

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors).Count().IsEqualTo(1);
        await Assert.That(result.Warnings).IsEmpty();
        await Assert.That(result.Infos).IsEmpty();
    }

    [Test]
    public async Task Constructor_Handles_Single_Severity_And_Empty_Inputs()
    {
        var warningResult = new ValidationResult(
        [
            new ValidationFailure("First", "First", "first", ValiantSeverity.Warning),
            new ValidationFailure("Second", "Second", "second", ValiantSeverity.Warning)
        ]);
        var infoResult = new ValidationResult(
        [
            new ValidationFailure("First", "First", "first", ValiantSeverity.Info),
            new ValidationFailure("Second", "Second", "second", ValiantSeverity.Info)
        ]);
        var emptyResult = new ValidationResult([]);

        await Assert.That(warningResult.IsValid).IsTrue();
        await Assert.That(warningResult.Warnings).Count().IsEqualTo(2);
        await Assert.That(infoResult.IsValid).IsTrue();
        await Assert.That(infoResult.Infos).Count().IsEqualTo(2);
        await Assert.That(emptyResult.Failures).IsEmpty();
        await Assert.That(emptyResult.Errors).IsEmpty();
        await Assert.That(emptyResult.Warnings).IsEmpty();
        await Assert.That(emptyResult.Infos).IsEmpty();
    }

    [Test]
    public async Task Builder_Creates_Stable_Results_For_Empty_Single_And_Multiple_Failures()
    {
        var emptyBuilder = new ValidationResultBuilder(0);
        var empty = emptyBuilder.Build();

        var singleBuilder = new ValidationResultBuilder(5);
        singleBuilder.Add(new ValidationFailure("Name", "Required", "required"));
        var single = singleBuilder.Build();

        var mixedBuilder = new ValidationResultBuilder(2);
        mixedBuilder.Add(new ValidationFailure("First", "Warning", "warning", ValiantSeverity.Warning));
        mixedBuilder.Add(new ValidationFailure("Second", "Error", "error"));
        mixedBuilder.Add(new ValidationFailure("Third", "Info", "info", ValiantSeverity.Info));
        var mixed = mixedBuilder.Build();

        await Assert.That(empty).IsSameReferenceAs(ValidationResult.Valid);
        await Assert.That(singleBuilder.Build()).IsSameReferenceAs(single);
        await Assert.That(single.Failures).Count().IsEqualTo(1);
        await Assert.That(single.Errors[0].ErrorCode).IsEqualTo("Required");
        await Assert.That(mixedBuilder.Build()).IsSameReferenceAs(mixed);
        await Assert.That(mixed.Failures.Select(static failure => failure.PropertyName))
            .IsEquivalentTo(["First", "Second", "Third"]);
        await Assert.That(mixed.Errors[0].PropertyName).IsEqualTo("Second");
        await Assert.That(mixed.Warnings[0].PropertyName).IsEqualTo("First");
        await Assert.That(mixed.Infos[0].PropertyName).IsEqualTo("Third");
    }

    [Test]
    public async Task Builder_Rejects_Adding_After_Result_Is_Built()
    {
        var builder = new ValidationResultBuilder(1);
        builder.Add(new ValidationFailure("Name", "Required", "required"));
        _ = builder.Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(
            () => builder.Add(new ValidationFailure("Name", "Second", "second"))));
    }

    [Test]
    public async Task Result_Lists_Do_Not_Expose_Their_Owned_Arrays()
    {
        var result = new ValidationResult(
        [
            new ValidationFailure("Error", "Error", "error"),
            new ValidationFailure("Warning", "Warning", "warning", ValiantSeverity.Warning),
            new ValidationFailure("Info", "Info", "info", ValiantSeverity.Info)
        ]);

        await Assert.That(result.Failures as ValidationFailure[]).IsNull();
        await Assert.That(result.Errors as ValidationFailure[]).IsNull();
        await Assert.That(result.Warnings as ValidationFailure[]).IsNull();
        await Assert.That(result.Infos as ValidationFailure[]).IsNull();
    }
}
