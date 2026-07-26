using MediatR;

namespace StaticNorth.Valiant.Validation.MediatR;

/// <summary>
/// MediatR pipeline behavior that validates requests before invoking the next pipeline step.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class ValiantValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Validates the request and invokes the next pipeline step when validation succeeds.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="next">The next pipeline step.</param>
    /// <param name="cancellationToken">A cancellation token used to cancel validation.</param>
    /// <returns>The response from the next pipeline step.</returns>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _ = next ?? throw new ArgumentNullException(nameof(next));
        await ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        return await next(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ValidateAsync(TRequest request, CancellationToken cancellationToken)
    {
        List<ValidationFailure>? failures = null;
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
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
