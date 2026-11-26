using BenchmarkDotNet.Attributes;

namespace StaticNorth.Valiant.Validation.Benchmarks;

[Config(typeof(RuntimeAsyncConfig))]
[MemoryDiagnoser]
[OperationsPerSecond]
public class RuntimeAsyncBenchmarks
{
    private RuntimeAsyncValidationInput _input = null!;
    private RuntimeAsyncRule _rule = null!;
    private RuntimeAsyncSingleValidator _single = null!;
    private RuntimeAsyncNestedValidator _nested = null!;

    [Params(false, true)]
    public bool Suspend { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        RuntimeAsyncConfig.AssertValidatorLowering();

        var options = new ValiantValidationOptions { Mode = ValiantValidationMode.Fast };
        _rule = new RuntimeAsyncRule(Suspend);
        _input = new RuntimeAsyncValidationInput(_rule);
        _single = new RuntimeAsyncSingleValidator(options);
        _nested = new RuntimeAsyncNestedValidator(_single, options);

        await AssertCompletion(_single.ValidateAsync(_input)).ConfigureAwait(false);
        await AssertCompletion(_nested.ValidateAsync(_input)).ConfigureAwait(false);
    }

    [Benchmark]
    public ValueTask<ValidationResult> SingleValidator()
    {
        var validation = _single.ValidateAsync(_input);
        if (Suspend)
        {
            _rule.Complete();
        }

        return validation;
    }

    [Benchmark]
    public ValueTask<ValidationResult> NestedValidator()
    {
        var validation = _nested.ValidateAsync(_input);
        if (Suspend)
        {
            _rule.Complete();
        }

        return validation;
    }

    private async Task AssertCompletion(ValueTask<ValidationResult> validation)
    {
        if (validation.IsCompleted == Suspend)
        {
            throw new InvalidOperationException("The validator completed differently than the benchmark requires.");
        }

        if (Suspend)
        {
            _rule.Complete();
        }

        if (!(await validation.ConfigureAwait(false)).IsValid)
        {
            throw new InvalidOperationException("The benchmark input must pass validation.");
        }
    }
}
