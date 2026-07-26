using Mediator;

namespace StaticNorth.Valiant.Validation.Mediator;

/// <summary>
/// Mediator pipeline behavior that validates messages before invoking the next pipeline step.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class ValiantValidationBehavior<TMessage, TResponse>(
    IEnumerable<IValidator<TMessage>> validators) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    /// <summary>
    /// Validates the message and invokes the next pipeline step when validation succeeds.
    /// </summary>
    /// <param name="message">The message to validate.</param>
    /// <param name="next">The next pipeline step.</param>
    /// <param name="cancellationToken">A cancellation token used to cancel validation.</param>
    /// <returns>The response from the next pipeline step.</returns>
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        _ = next ?? throw new ArgumentNullException(nameof(next));
        await ValidateAsync(message, cancellationToken).ConfigureAwait(false);
        return await next(message, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ValidateAsync(TMessage message, CancellationToken cancellationToken)
    {
        List<ValidationFailure>? failures = null;
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(message, cancellationToken).ConfigureAwait(false);
            AddFailures(ref failures, result);
        }

        if (failures is not null)
        {
            new ValidationResult(failures).ThrowIfFailed();
        }
    }

    private static void AddFailures(ref List<ValidationFailure>? failures, ValidationResult result)
    {
        if (result.Failures.Count == 0)
        {
            return;
        }

        failures ??= [];
        failures.AddRange(result.Failures);
    }
}
