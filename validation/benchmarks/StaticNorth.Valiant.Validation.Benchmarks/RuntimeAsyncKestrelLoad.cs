using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace StaticNorth.Valiant.Validation.Benchmarks;

internal static class RuntimeAsyncKestrelLoad
{
    private const int WarmupRequests = 80_000;
    private const int MeasuredRequests = 160_000;
    private const int Trials = 4;

    internal static async Task RunAsync(string runtimeAsyncAssembly)
    {
        RuntimeAsyncConfig.AssertValidatorLowering(runtimeAsyncEnabled: false);
        var classicAssembly = typeof(RuntimeAsyncKestrelLoad).Assembly.Location;
        Console.WriteLine("concurrency,nested,suspend,trial,runtime_async,requests,requests_per_second,p95_us,allocated_bytes_per_request");

        foreach (var concurrency in new[] { 8, 32 })
        foreach (var nested in new[] { false, true })
        foreach (var suspend in new[] { false, true })
        {
            for (var trial = 0; trial < Trials; trial++)
            {
                for (var variant = 0; variant < 2; variant++)
                {
                    // Alternate which build runs first in each pair.
                    var runtimeAsync = (trial + variant) % 2 != 0;
                    var assembly = runtimeAsync ? runtimeAsyncAssembly : classicAssembly;
                    await RunTrialAsync(assembly, concurrency, nested, suspend, trial + 1, runtimeAsync)
                        .ConfigureAwait(false);
                }
            }
        }
    }

    internal static async Task ServeAsync(bool nested, bool suspend, bool runtimeAsync)
    {
        RuntimeAsyncConfig.AssertValidatorLowering(runtimeAsync);
        await using var endpoint = new RuntimeAsyncEndpoint(nested, suspend);
        await endpoint.StartAsync().ConfigureAwait(false);
        Console.WriteLine(endpoint.Address);

        while (await Console.In.ReadLineAsync().ConfigureAwait(false) == "allocations")
        {
            var allocated = GC.GetTotalAllocatedBytes(precise: true);
            Console.WriteLine(allocated.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static async Task RunTrialAsync(
        string assembly, int concurrency, bool nested, bool suspend, int trial, bool runtimeAsync)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            ArgumentList = { assembly, "--runtime-async-kestrel-server", nested.ToString(), suspend.ToString(), runtimeAsync.ToString() }
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Kestrel.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            var address = await ReadLineAsync(process, deadline.Token).ConfigureAwait(false);
            using var client = new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = concurrency })
            {
                BaseAddress = new Uri(address),
                Timeout = TimeSpan.FromSeconds(10)
            };
            var latencies = new long[MeasuredRequests];
            await SendRequestsAsync(client, concurrency, WarmupRequests, null).ConfigureAwait(false);

            var initialBytes = await ReadAllocatedBytesAsync(process, deadline.Token).ConfigureAwait(false);
            var start = Stopwatch.GetTimestamp();
            await SendRequestsAsync(client, concurrency, MeasuredRequests, latencies).ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(start);
            var finalBytes = await ReadAllocatedBytesAsync(process, deadline.Token).ConfigureAwait(false);

            Array.Sort(latencies);
            var p95 = Stopwatch.GetElapsedTime(0, latencies[(int)Math.Ceiling(latencies.Length * 0.95) - 1]);
            var requestsPerSecond = MeasuredRequests / elapsed.TotalSeconds;
            var bytesPerRequest = (double)(finalBytes - initialBytes) / MeasuredRequests;

            process.StandardInput.Close();
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            if (process.ExitCode != 0 || bytesPerRequest <= 0 || p95 <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("Kestrel did not exit successfully with valid measurements.");
            }

            Console.WriteLine(FormattableString.Invariant(
                $"{concurrency},{nested},{suspend},{trial},{runtimeAsync},{MeasuredRequests},{requestsPerSecond:F2},{p95.TotalMicroseconds:F2},{bytesPerRequest:F2}"));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
    }

    private static Task SendRequestsAsync(HttpClient client, int concurrency, int count, long[]? latencies)
    {
        var workers = new Task[concurrency];
        for (var worker = 0; worker < concurrency; worker++)
        {
            var offset = worker * count / concurrency;
            var end = (worker + 1) * count / concurrency;
            workers[worker] = SendWorkerRequestsAsync(client, offset, end - offset, latencies);
        }

        return Task.WhenAll(workers);
    }

    private static async Task SendWorkerRequestsAsync(HttpClient client, int offset, int count, long[]? latencies)
    {
        for (var index = 0; index < count; index++)
        {
            var start = Stopwatch.GetTimestamp();
            using var response = await client.GetAsync(RuntimeAsyncEndpoint.RequestPath).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                throw new InvalidOperationException($"Expected HTTP 204, received HTTP {(int)response.StatusCode}.");
            }

            if (latencies is not null)
            {
                latencies[offset + index] = Stopwatch.GetTimestamp() - start;
            }
        }
    }

    private static async Task<long> ReadAllocatedBytesAsync(Process process, CancellationToken cancellationToken)
    {
        await process.StandardInput.WriteLineAsync("allocations".AsMemory(), cancellationToken).ConfigureAwait(false);
        var line = await ReadLineAsync(process, cancellationToken).ConfigureAwait(false);
        return long.Parse(line, CultureInfo.InvariantCulture);
    }

    private static async Task<string> ReadLineAsync(Process process, CancellationToken cancellationToken)
    {
        return await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Kestrel exited before sending benchmark data.");
    }
}
