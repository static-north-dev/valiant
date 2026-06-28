using System.Reflection;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Valiant.Validation.AspNetCore;

/// <summary>
/// Minimal API validation extensions for Valiant validators.
/// </summary>
public static class ValiantMinimalApiExtensions
{
    /// <summary>
    /// Adds an endpoint filter that validates all endpoint arguments with registered Valiant validators.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder WithValiantValidation(this RouteHandlerBuilder builder)
    {
        _ = builder ?? throw new ArgumentNullException(nameof(builder));

        return builder
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .AddEndpointFilterFactory((factoryContext, next) =>
        {
            var candidates = GetValidationCandidates(factoryContext.MethodInfo.GetParameters());
            if (candidates.Length == 0)
            {
                return next;
            }

            return async invocationContext =>
            {
                var failures = new List<ValidationFailure>();
                foreach (var candidate in candidates)
                {
                    if (candidate.ArgumentIndex >= invocationContext.Arguments.Count)
                    {
                        continue;
                    }

                    var model = invocationContext.Arguments[candidate.ArgumentIndex];
                    if (model is null)
                    {
                        continue;
                    }

                    foreach (var validator in ValiantValidatorExecutor.GetValidators(invocationContext.HttpContext.RequestServices, candidate.ModelType))
                    {
                        var result = await ValiantValidatorExecutor
                            .ValidateAsync(candidate.ModelType, validator, model, invocationContext.HttpContext.RequestAborted)
                            .ConfigureAwait(false);
                        failures.AddRange(result.Errors);
                    }
                }

                return failures.Count == 0
                    ? await next(invocationContext).ConfigureAwait(false)
                    : Results.ValidationProblem(ValiantProblemDetails.ToValidationProblemDictionary(failures));
            };
        });
    }

    /// <summary>
    /// Adds an endpoint filter that validates endpoint arguments assignable to <typeparamref name="T" /> with registered Valiant validators.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <typeparam name="T">The model type to validate.</typeparam>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder WithValiantValidation<T>(this RouteHandlerBuilder builder)
    {
        _ = builder ?? throw new ArgumentNullException(nameof(builder));

        return builder
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .AddEndpointFilter(async (invocationContext, next) =>
        {
            var failures = new List<ValidationFailure>();
            var validators = invocationContext.HttpContext.RequestServices.GetServices<IValidator<T>>().ToArray();
            if (validators.Length == 0)
            {
                return await next(invocationContext).ConfigureAwait(false);
            }

            foreach (var argument in invocationContext.Arguments)
            {
                if (argument is not T model)
                {
                    continue;
                }

                foreach (var validator in validators)
                {
                    var result = await validator.ValidateAsync(model, invocationContext.HttpContext.RequestAborted).ConfigureAwait(false);
                    failures.AddRange(result.Errors);
                }
            }

            return failures.Count == 0
                ? await next(invocationContext).ConfigureAwait(false)
                : Results.ValidationProblem(ValiantProblemDetails.ToValidationProblemDictionary(failures));
        });
    }

    private static ValidationCandidate[] GetValidationCandidates(ParameterInfo[] parameters)
    {
        var candidates = new List<ValidationCandidate>(parameters.Length);
        for (var index = 0; index < parameters.Length; index++)
        {
            var type = parameters[index].ParameterType;
            if (ShouldSkipParameter(type))
            {
                continue;
            }

            candidates.Add(new ValidationCandidate(index, type));
        }

        return candidates.ToArray();
    }

    private static bool ShouldSkipParameter(Type type)
    {
        return type == typeof(HttpContext) ||
            type == typeof(HttpRequest) ||
            type == typeof(HttpResponse) ||
            type == typeof(CancellationToken) ||
            type == typeof(Stream) ||
            type == typeof(PipeReader);
    }

    private readonly record struct ValidationCandidate(int ArgumentIndex, Type ModelType);
}
