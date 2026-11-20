namespace StaticNorth.Valiant.Validation;

internal readonly struct ValidationContextDeferredValidation(
    object? synchronousCheck,
    Func<CancellationToken, ValueTask>? validateAsync)
{
    public bool RequiresAsync => synchronousCheck is null;

    public Func<CancellationToken, ValueTask>? ValidateAsync { get; } = validateAsync;

    public void Execute()
    {
        if (synchronousCheck is Action validate)
        {
            validate();
        }
        else
        {
            ((IDeferredValidationCheck)synchronousCheck!).Execute();
        }
    }
}
