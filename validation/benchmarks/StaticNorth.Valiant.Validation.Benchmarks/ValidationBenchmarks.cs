using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using StaticNorth.Valiant.Validation;

namespace StaticNorth.Valiant.Validation.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ValidationBenchmarks
{
    private BenchmarkValidator _valiantValidator = null!;
    private ValiantValidationModel _valiantModel = null!;
    private ValidlyValidationModel _validlyModel = null!;

    [Params(
        ValidationScenario.Valid,
        ValidationScenario.OneFailure,
        ValidationScenario.AllFailures)]
    public ValidationScenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _valiantValidator = new BenchmarkValidator(new ValiantValidationOptions
        {
            Mode = ValiantValidationMode.Fast,
            FailureBehavior = ValiantFailureBehavior.ValidateAll,
        });

        (_valiantModel, _validlyModel) = CreateModels(Scenario);
        AssertEquivalentResults();
    }

    [Benchmark(Baseline = true)]
    public bool Validly()
    {
        using var result = _validlyModel.Validate();
        return result.IsSuccess;
    }

    [Benchmark]
    public bool ValiantFast()
    {
        return _valiantValidator.Validate(_valiantModel).IsValid;
    }

    private static (ValiantValidationModel Valiant, ValidlyValidationModel Validly) CreateModels(
        ValidationScenario scenario)
    {
        var values = scenario switch
        {
            ValidationScenario.Valid => new ModelValues(
                "Ada Lovelace",
                "ada_lovelace",
                "GB",
                36,
                "Ada"),
            ValidationScenario.OneFailure => new ModelValues(
                null,
                "ada_lovelace",
                "GB",
                36,
                "Ada"),
            ValidationScenario.AllFailures => new ModelValues(
                null,
                "usr",
                "USA",
                16,
                ""),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

        return (
            new ValiantValidationModel
            {
                DisplayName = values.DisplayName,
                Username = values.Username,
                CountryCode = values.CountryCode,
                Age = values.Age,
                Nickname = values.Nickname,
            },
            new ValidlyValidationModel
            {
                DisplayName = values.DisplayName,
                Username = values.Username,
                CountryCode = values.CountryCode,
                Age = values.Age,
                Nickname = values.Nickname,
            });
    }

    private void AssertEquivalentResults()
    {
        string[] expectedProperties = Scenario switch
        {
            ValidationScenario.Valid => [],
            ValidationScenario.OneFailure => [nameof(ValiantValidationModel.DisplayName)],
            ValidationScenario.AllFailures =>
            [
                nameof(ValiantValidationModel.Age),
                nameof(ValiantValidationModel.CountryCode),
                nameof(ValiantValidationModel.DisplayName),
                nameof(ValiantValidationModel.Nickname),
                nameof(ValiantValidationModel.Username),
            ],
            _ => throw new ArgumentOutOfRangeException(),
        };

        var valiantResult = _valiantValidator.Validate(_valiantModel);
        var valiantProperties = valiantResult.Errors
            .Select(static failure => failure.PropertyName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        using var validlyResult = _validlyModel.Validate();
        var validlyProperties = validlyResult.Properties
            .SelectMany(static property => property.Messages.Select(_ => property.PropertyPath))
            .Order(StringComparer.Ordinal)
            .ToArray();

        AssertResult(
            nameof(ValiantFast),
            valiantResult.IsValid,
            valiantProperties,
            expectedProperties);
        AssertResult(
            nameof(Validly),
            validlyResult.IsSuccess,
            validlyProperties,
            expectedProperties);
    }

    private static void AssertResult(
        string implementation,
        bool isValid,
        string[] actualProperties,
        string[] expectedProperties)
    {
        var expectedValidity = expectedProperties.Length == 0;
        if (isValid != expectedValidity
            || !actualProperties.SequenceEqual(expectedProperties, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{implementation} produced validity {isValid} and failures "
                + $"[{string.Join(", ", actualProperties)}], expected validity "
                + $"{expectedValidity} and failures [{string.Join(", ", expectedProperties)}].");
        }
    }

    private sealed record ModelValues(
        string? DisplayName,
        string Username,
        string CountryCode,
        int Age,
        string Nickname);
}
