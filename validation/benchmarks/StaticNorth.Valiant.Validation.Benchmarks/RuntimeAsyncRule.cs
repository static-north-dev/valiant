using System.Threading.Tasks.Sources;

namespace StaticNorth.Valiant.Validation.Benchmarks;

public sealed class RuntimeAsyncRule(bool suspend, bool completeOnThreadPool = false, bool isValid = true) : IValueTaskSource<bool>
{
    private ManualResetValueTaskSourceCore<bool> _completion;

    public ValueTask<bool> Evaluate()
    {
        if (!suspend)
        {
            return ValueTask.FromResult(isValid);
        }

        // One operation is in flight at a time and the benchmark completes it after validation yields.
        _completion.Reset();
        return new ValueTask<bool>(this, _completion.Version);
    }

    public void Complete() => _completion.SetResult(isValid);

    bool IValueTaskSource<bool>.GetResult(short token) => _completion.GetResult(token);

    ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token) => _completion.GetStatus(token);

    void IValueTaskSource<bool>.OnCompleted(
        Action<object?> continuation,
        object? state,
        short token,
        ValueTaskSourceOnCompletedFlags flags)
    {
        _completion.OnCompleted(continuation, state, token, flags);
        if (completeOnThreadPool)
        {
            ThreadPool.UnsafeQueueUserWorkItem(static rule => rule.Complete(), this, preferLocal: false);
        }
    }
}
