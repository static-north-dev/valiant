using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using StaticNorth.Valiant.Validation;

namespace StaticNorth.Valiant.Validation.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ValidationResultConstructionBenchmarks
{
    private IReadOnlyList<ValidationFailure> _failures = null!;

    [Params(
        ValidationResultScenario.OneError,
        ValidationResultScenario.FiveErrors,
        ValidationResultScenario.MixedSeverities)]
    public ValidationResultScenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _failures = Scenario switch
        {
            ValidationResultScenario.OneError =>
            [
                new ValidationFailure("Name", "Required", "'Name' is required.")
            ],
            ValidationResultScenario.FiveErrors =>
            [
                new ValidationFailure("Name", "Required", "'Name' is required."),
                new ValidationFailure("Username", "MinLength", "'Username' must be at least 5 characters."),
                new ValidationFailure("CountryCode", "MaxLength", "'CountryCode' must be at most 2 characters."),
                new ValidationFailure("Age", "InclusiveBetween", "'Age' must be between 18 and 99."),
                new ValidationFailure("Nickname", "NotEmpty", "'Nickname' must not be empty.")
            ],
            ValidationResultScenario.MixedSeverities =>
            [
                new ValidationFailure("Name", "Required", "'Name' is required."),
                new ValidationFailure("Username", "Warning", "'Username' is unusual.", ValiantSeverity.Warning),
                new ValidationFailure("CountryCode", "Info", "'CountryCode' was normalized.", ValiantSeverity.Info),
                new ValidationFailure("Age", "InclusiveBetween", "'Age' must be between 18 and 99."),
                new ValidationFailure("Nickname", "Warning", "'Nickname' is unusual.", ValiantSeverity.Warning)
            ],
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    [Benchmark]
    public ValidationResult Construct()
    {
        return new ValidationResult(_failures);
    }
}

public enum ValidationResultScenario
{
    OneError,
    FiveErrors,
    MixedSeverities,
}
