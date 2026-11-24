using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;

namespace StaticNorth.Valiant.Validation.Benchmarks;

[Config(typeof(RuntimeAsyncConfig))]
[MemoryDiagnoser]
[OperationsPerSecond]
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "BenchmarkDotNet calls GlobalCleanup to dispose the client and host.")]
public class RuntimeAsyncEndpointBenchmarks
{
    private RuntimeAsyncEndpoint _endpoint = null!;
    private HttpClient _client = null!;

    [Params(false, true)]
    public bool Nested { get; set; }

    [Params(false, true)]
    public bool Suspend { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        RuntimeAsyncConfig.AssertValidatorLowering();
        _endpoint = new RuntimeAsyncEndpoint(Nested, Suspend);
        await _endpoint.StartAsync().ConfigureAwait(false);
        _client = new HttpClient { BaseAddress = _endpoint.Address };
    }

    [Benchmark]
    public Task<string> ValidateRequest() => _client.GetStringAsync(RuntimeAsyncEndpoint.RequestPath);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _client?.Dispose();
        if (_endpoint is not null)
        {
            await _endpoint.DisposeAsync().ConfigureAwait(false);
        }
    }
}
