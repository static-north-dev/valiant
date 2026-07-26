namespace StaticNorth.Valiant.Validation;

/// <summary>
/// Base type for reusable property validators.
/// </summary>
/// <typeparam name="T">The property value type being validated.</typeparam>
public abstract class PropertyValidatorDefinition<T> : IPropertyValidator<T>
{
    [ThreadStatic]
    private static List<PropertyValidatorDefinition<T>>? bridgeStack;

    /// <summary>
    /// Validates the supplied property value.
    /// </summary>
    /// <param name="value">The property value to validate.</param>
    /// <returns>The validation result.</returns>
    public virtual ValidationResult Validate(T value)
    {
        EnterBridge();
        try
        {
            return ValidateAsync(value).AsTask().GetAwaiter().GetResult();
        }
        finally
        {
            ExitBridge();
        }
    }

    /// <summary>
    /// Validates the supplied property value, including asynchronous validations.
    /// </summary>
    /// <param name="value">The property value to validate.</param>
    /// <param name="cancellationToken">A cancellation token for asynchronous validations.</param>
    /// <returns>The validation result.</returns>
    public virtual ValueTask<ValidationResult> ValidateAsync(T value, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        EnterBridge();
        try
        {
            return new ValueTask<ValidationResult>(Validate(value));
        }
        finally
        {
            ExitBridge();
        }
    }

    private void EnterBridge()
    {
        bridgeStack ??= new List<PropertyValidatorDefinition<T>>();
        foreach (var validator in bridgeStack)
        {
            if (ReferenceEquals(validator, this))
            {
                throw new InvalidOperationException(
                    $"Property validator '{GetType().FullName}' must override Validate or ValidateAsync.");
            }
        }

        bridgeStack.Add(this);
    }

    private void ExitBridge()
    {
        if (bridgeStack is null)
        {
            return;
        }

        for (var index = bridgeStack.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(bridgeStack[index], this))
            {
                bridgeStack.RemoveAt(index);
                break;
            }
        }
    }
}
