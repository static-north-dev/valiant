using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using StaticNorth.Valiant.Validation;
using Validly;

namespace StaticNorth.Valiant.Validation.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ValidationStatusBenchmarks
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

        var values = Scenario switch
        {
            ValidationScenario.Valid => new ModelValues("Ada Lovelace", "ada_lovelace", "GB", 36, "Ada"),
            ValidationScenario.OneFailure => new ModelValues(null, "ada_lovelace", "GB", 36, "Ada"),
            ValidationScenario.AllFailures => new ModelValues(null, "usr", "USA", 16, ""),
            _ => throw new ArgumentOutOfRangeException(),
        };
        _valiantModel = new ValiantValidationModel
        {
            DisplayName = values.DisplayName,
            Username = values.Username,
            CountryCode = values.CountryCode,
            Age = values.Age,
            Nickname = values.Nickname,
        };
        _validlyModel = new ValidlyValidationModel
        {
            DisplayName = values.DisplayName,
            Username = values.Username,
            CountryCode = values.CountryCode,
            Age = values.Age,
            Nickname = values.Nickname,
        };

        using var validlyResult = _validlyModel.Validate();
        if (_valiantValidator.IsValid(_valiantModel) != validlyResult.IsSuccess)
        {
            throw new InvalidOperationException("The status benchmark validators produced different results.");
        }
    }

    [Benchmark(Baseline = true)]
    public bool ValidlyStatus()
    {
        using var result = _validlyModel.Validate();
        return result.IsSuccess;
    }

    [Benchmark]
    public bool ValiantGeneratedStatus()
    {
        return _valiantValidator.IsValid(_valiantModel);
    }

    private sealed record ModelValues(
        string? DisplayName,
        string Username,
        string CountryCode,
        int Age,
        string Nickname);
}
