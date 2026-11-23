namespace StaticNorth.Valiant.Validation;

// Item contexts share the deferred rule queue and its completion guard.
internal sealed class ValidationContextExecution : List<ValidationContextDeferredValidation>
{
    private int _started;

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("A validation context can be completed only once. Create a new context to run validation again.");
        }
    }
}
