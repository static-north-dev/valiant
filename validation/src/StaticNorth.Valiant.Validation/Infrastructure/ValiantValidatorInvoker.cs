using System.ComponentModel;

namespace StaticNorth.Valiant.Validation.Infrastructure;

/// <summary>
/// Invokes every validator registered for an exact model type without runtime generic construction.
/// </summary>
/// <typeparam name="T">The model type to validate.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ValiantValidatorInvoker<T> : IValiantValidatorInvoker
{
    /// <inheritdoc />
    public Type ModelType => typeof(T);

    /// <inheritdoc />
    public object ResolveValidators(IServiceProvider services)
    {
        _ = services ?? throw new ArgumentNullException(nameof(services));

        var validators = services.GetService(typeof(IEnumerable<IValidator<T>>)) as IEnumerable<IValidator<T>>;
        return validators?.ToArray() ?? [];
    }

    /// <inheritdoc />
    public async ValueTask<ValidationResult> ValidateAsync(
        object validators,
        object model,
        CancellationToken cancellationToken)
    {
        _ = validators ?? throw new ArgumentNullException(nameof(validators));
        _ = model ?? throw new ArgumentNullException(nameof(model));

        var typedValidators = (IValidator<T>[])validators;
        var typedModel = (T)model;
        ValidationResult? singleResult = null;
        ValidationResultBuilder? combinedResults = null;

        foreach (var validator in typedValidators)
        {
            var result = await validator.ValidateAsync(typedModel, cancellationToken).ConfigureAwait(false);
            if (result.Failures.Count == 0)
            {
                continue;
            }

            if (singleResult is null)
            {
                singleResult = result;
                continue;
            }

            if (combinedResults is null)
            {
                combinedResults = new ValidationResultBuilder(
                    singleResult.Failures.Count + result.Failures.Count);
                combinedResults.AddRange(singleResult.Failures);
            }

            combinedResults.AddRange(result.Failures);
        }

        return combinedResults?.Build() ?? singleResult ?? ValidationResult.Valid;
    }
}
