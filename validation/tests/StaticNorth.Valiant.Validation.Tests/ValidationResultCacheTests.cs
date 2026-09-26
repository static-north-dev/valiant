using StaticNorth.Valiant.Validation.Infrastructure;

namespace StaticNorth.Valiant.Validation.Tests;

public sealed class ValidationResultCacheTests
{
    [Test]
    [Arguments(5, 6_000)]
    [Arguments(6, 13_000)]
    public async Task Cold_Allocation_For_All_Combinations_Is_Bounded(int count, int maximumBytes)
    {
        var failures = Enumerable.Range(0, count)
            .Select(static index => new ValidationFailure("Property", "Required", "required"))
            .ToArray();
        GC.KeepAlive(Populate());
        var before = GC.GetAllocatedBytesForCurrentThread();
        var cache = Populate();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(cache);

        await Assert.That(allocated).IsLessThanOrEqualTo((long)maximumBytes);

        ValidationResultCache Populate()
        {
            var result = new ValidationResultCache(failures);
            for (var mask = 0; mask < (1 << count); mask++)
            {
                GC.KeepAlive(result.Get(mask));
            }

            return result;
        }
    }

    [Test]
    public async Task Every_Combination_Preserves_Order_Partitions_And_Retained_Results()
    {
        ValidationFailure[] failures =
        [
            new("Name", "Required", "required"),
            new("Age", "Range", "range", ValiantSeverity.Warning),
            new("Code", "Length", "length", ValiantSeverity.Info),
            new("Other", "Unknown", "unknown", (ValiantSeverity)42)
        ];
        var cache = new ValidationResultCache(failures);
        var retained = Enumerable.Range(0, 16).Select(cache.Get).ToArray();
        failures[0] = new ValidationFailure("Changed", "Changed", "changed");

        for (var mask = 0; mask < retained.Length; mask++)
        {
            var expected = Enumerable.Range(0, 4)
                .Where(index => (mask & (1 << index)) != 0)
                .Select(index => new[] { "Name", "Age", "Code", "Other" }[index]);
            var result = retained[mask];
            await Assert.That(ReferenceEquals(cache.Get(mask), result)).IsTrue();
            await Assert.That(string.Join(",", result.Failures.Select(static failure => failure.PropertyName))).IsEqualTo(string.Join(",", expected));
            await Assert.That(result.IsValid).IsEqualTo((mask & 9) == 0);
            await Assert.That(result.Errors.Count).IsEqualTo(((mask & 1) != 0 ? 1 : 0) + ((mask & 8) != 0 ? 1 : 0));
            await Assert.That(result.Warnings.Count).IsEqualTo((mask & 2) != 0 ? 1 : 0);
            await Assert.That(result.Infos.Count).IsEqualTo((mask & 4) != 0 ? 1 : 0);
        }
    }

    [Test]
    public async Task Concurrent_Cold_Reads_Publish_One_Immutable_Result_Per_Combination()
    {
        var cache = new ValidationResultCache(
        [
            new("Name", "Required", "required"),
            new("Code", "Required", "required")
        ]);
        var results = new ValidationResult[256];
        Parallel.For(0, results.Length, index => results[index] = cache.Get(index % 4));

        for (var index = 0; index < results.Length; index++)
        {
            await Assert.That(ReferenceEquals(results[index], cache.Get(index % 4))).IsTrue();
        }
    }

    [Test]
    [Arguments(-1)]
    [Arguments(2)]
    public async Task Invalid_Masks_Are_Rejected(int mask)
    {
        var cache = new ValidationResultCache([new("Name", "Required", "required")]);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.Run(() => cache.Get(mask)));
    }

    [Test]
    [Arguments(0)]
    [Arguments(7)]
    public async Task Cache_Size_Is_Bounded(int count)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.Run(() =>
            new ValidationResultCache(new ValidationFailure[count])));
    }
}
