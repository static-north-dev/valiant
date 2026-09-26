using BenchmarkDotNet.Attributes;

namespace StaticNorth.Valiant.Validation.Benchmarks;

[MemoryDiagnoser]
public class ValidationFailurePatternBenchmarks
{
    private BenchmarkValidator _validator = null!;
    private ValiantValidationModel[] _valiantModels = null!;
    private ValidlyValidationModel[] _validlyModels = null!;
    private int _valiantIndex;
    private int _validlyIndex;

    [Params(2, 32)]
    public int PatternCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _validator = new BenchmarkValidator(new ValiantValidationOptions { Mode = ValiantValidationMode.Fast });
        _valiantModels = new ValiantValidationModel[PatternCount];
        _validlyModels = new ValidlyValidationModel[PatternCount];
        for (var index = 0; index < PatternCount; index++)
        {
            var mask = PatternCount == 2 ? (index == 0 ? 1 : 31) : index;
            var name = (mask & 1) != 0 ? null : "Ada";
            var username = (mask & 2) != 0 ? "usr" : "ada_lovelace";
            var country = (mask & 4) != 0 ? "USA" : "GB";
            var age = (mask & 8) != 0 ? 16 : 36;
            var nickname = (mask & 16) != 0 ? "" : "Ada";
            _valiantModels[index] = new ValiantValidationModel
            {
                DisplayName = name, Username = username, CountryCode = country, Age = age, Nickname = nickname
            };
            _validlyModels[index] = new ValidlyValidationModel
            {
                DisplayName = name, Username = username, CountryCode = country, Age = age, Nickname = nickname
            };

            var valiant = _validator.Validate(_valiantModels[index]);
            using var validly = _validlyModels[index].Validate();
            var valiantProperties = valiant.Errors.Select(static failure => failure.PropertyName).Order(StringComparer.Ordinal);
            var validlyProperties = validly.Properties.SelectMany(static property => property.Messages.Select(_ => property.PropertyPath)).Order(StringComparer.Ordinal);
            var expectedCount = 0;
            for (var bits = mask; bits != 0; bits &= bits - 1)
            {
                expectedCount++;
            }

            if (valiant.IsValid != validly.IsSuccess || valiant.Errors.Count != expectedCount ||
                !valiantProperties.SequenceEqual(validlyProperties, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Validation results differ for failure mask {mask}.");
            }
        }
    }

    [Benchmark(Baseline = true)]
    public bool Validly()
    {
        _validlyIndex = (_validlyIndex + 1) & (PatternCount - 1);
        using var result = _validlyModels[_validlyIndex].Validate();
        return result.IsSuccess;
    }

    [Benchmark]
    public bool ValiantFast()
    {
        _valiantIndex = (_valiantIndex + 1) & (PatternCount - 1);
        return _validator.Validate(_valiantModels[_valiantIndex]).IsValid;
    }
}
