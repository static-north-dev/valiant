using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StaticNorth.Valiant.Validation.Infrastructure;

namespace StaticNorth.Valiant.Validation.AspNetCore;

/// <summary>
/// Minimal API validation extensions for Valiant validators.
/// </summary>
public static class ValiantMinimalApiExtensions
{
    /// <summary>
    /// Adds an endpoint filter that validates each non-null endpoint argument with Valiant validators
    /// registered for its exact declared parameter type.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder WithValiantValidation(this RouteHandlerBuilder builder)
    {
        _ = builder ?? throw new ArgumentNullException(nameof(builder));

        return builder
            .ProducesValidationProblem()
            .AddEndpointFilterFactory(CreateValidationFilter);
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
            .ProducesValidationProblem()
            .AddEndpointFilter(ValidateTypedAsync<T>);
    }

    private static EndpointFilterDelegate CreateValidationFilter(
        EndpointFilterFactoryContext factoryContext,
        EndpointFilterDelegate next)
    {
        var plan = CreateValidationPlan(
            factoryContext.MethodInfo.GetParameters(),
            factoryContext.ApplicationServices);
        return plan.Candidates.Length == 0
            ? next
            : new ValiantValidationEndpointFilter(plan, next).InvokeAsync;
    }

    private static async ValueTask<object?> ValidateTypedAsync<T>(
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next)
    {
        // ASP.NET Core sets an error status before running filters when parameter binding fails.
        if (invocationContext.HttpContext.Response.StatusCode >= StatusCodes.Status400BadRequest)
        {
            return await next(invocationContext).ConfigureAwait(false);
        }

        List<ValidationFailure>? failures = null;
        var validators = invocationContext.HttpContext.RequestServices
            .GetServices<IValidator<T>>()
            .ToArray();

        foreach (var argument in invocationContext.Arguments)
        {
            if (argument is not T model)
            {
                continue;
            }

            foreach (var validator in validators)
            {
                var result = await validator.ValidateAsync(model, invocationContext.HttpContext.RequestAborted).ConfigureAwait(false);
                AddErrors(ref failures, result);
            }
        }

        return failures is null
            ? await next(invocationContext).ConfigureAwait(false)
            : new ValiantValidationProblemResult(failures);
    }

    private static async ValueTask<object?> ValidateCandidatesAsync(
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next,
        ValidationPlan plan)
    {
        if (invocationContext.HttpContext.Response.StatusCode >= StatusCodes.Status400BadRequest)
        {
            return await next(invocationContext).ConfigureAwait(false);
        }

        List<ValidationFailure>? failures = null;
        object?[]? resolvedValidators = null;

        foreach (var candidate in plan.Candidates)
        {
            var model = invocationContext.Arguments[candidate.ArgumentIndex];
            if (model is null)
            {
                continue;
            }

            resolvedValidators ??= new object?[plan.TypePlans.Length];
            var typePlan = plan.TypePlans[candidate.TypePlanIndex];
            var validators = resolvedValidators[candidate.TypePlanIndex];
            if (validators is null)
            {
                validators = typePlan.ResolveValidators(invocationContext.HttpContext.RequestServices);
                resolvedValidators[candidate.TypePlanIndex] = validators;
            }

            var candidateFailures = await typePlan
                .ValidateAsync(validators, model, invocationContext.HttpContext.RequestAborted)
                .ConfigureAwait(false);
            AddErrors(ref failures, candidateFailures);
        }

        return failures is null
            ? await next(invocationContext).ConfigureAwait(false)
            : new ValiantValidationProblemResult(failures);
    }

    private static void AddErrors(ref List<ValidationFailure>? failures, ValidationResult result)
    {
        if (result.Errors.Count == 0)
        {
            return;
        }

        failures ??= new List<ValidationFailure>();
        failures.AddRange(result.Errors);
    }

    private static ValidationPlan CreateValidationPlan(
        ParameterInfo[] parameters,
        IServiceProvider applicationServices)
    {
        var candidates = new List<ValidationCandidate>(parameters.Length);
        var typePlans = new List<ValidationTypePlan>();
        var typePlanIndices = new Dictionary<Type, int>();
        var generatedInvokers = GetGeneratedInvokers(applicationServices);

        for (var index = 0; index < parameters.Length; index++)
        {
            var type = parameters[index].ParameterType;
            if (!typePlanIndices.TryGetValue(type, out var typePlanIndex))
            {
                ValidationTypePlan? typePlan;
                if (generatedInvokers.TryGetValue(type, out var generatedInvoker))
                {
                    typePlan = new ValidationTypePlan(
                        generatedInvoker.ResolveValidators,
                        generatedInvoker.ValidateAsync);
                }
                else
                {
                    typePlan = CreateDynamicValidationTypePlanIfSupported(type);
                }

                if (typePlan is null)
                {
                    continue;
                }

                typePlanIndex = typePlans.Count;
                typePlanIndices.Add(type, typePlanIndex);
                typePlans.Add(typePlan);
            }

            candidates.Add(new ValidationCandidate(index, typePlanIndex));
        }

        return new ValidationPlan(candidates.ToArray(), typePlans.ToArray());
    }

    private static Dictionary<Type, IValiantValidatorInvoker> GetGeneratedInvokers(
        IServiceProvider applicationServices)
    {
        var generatedInvokers = new Dictionary<Type, IValiantValidatorInvoker>();
        foreach (var invoker in applicationServices.GetServices<IValiantValidatorInvoker>())
        {
            _ = generatedInvokers.TryAdd(invoker.ModelType, invoker);
        }

        return generatedInvokers;
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The reflection fallback is called only when the runtime reports that dynamic code is supported.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The reflection fallback is called only on JIT runtimes for compatibility with manually registered validators.")]
    private static ValidationTypePlan? CreateDynamicValidationTypePlanIfSupported(Type modelType)
    {
        return RuntimeFeature.IsDynamicCodeSupported
            ? CreateDynamicValidationTypePlan(modelType)
            : null;
    }

    [RequiresDynamicCode("Constructing a validator plan for an arbitrary runtime type requires dynamic generic code.")]
    [RequiresUnreferencedCode("The manually registered validator compatibility path uses reflection over a generic helper.")]
    private static ValidationTypePlan CreateDynamicValidationTypePlan(Type modelType)
    {
        var method = typeof(ValiantMinimalApiExtensions)
            .GetMethod(nameof(CreateValidationTypePlanCore), BindingFlags.NonPublic | BindingFlags.Static)!;

        return (ValidationTypePlan)method
            .MakeGenericMethod(modelType)
            .Invoke(null, null)!;
    }

    private static ValidationTypePlan CreateValidationTypePlanCore<T>()
    {
        return new ValidationTypePlan(
            static services => services.GetServices<IValidator<T>>().ToArray(),
            static (validators, model, cancellationToken) => ValidateModelAsync(
                (IValidator<T>[])validators,
                (T)model,
                cancellationToken));
    }

    private static async ValueTask<ValidationResult> ValidateModelAsync<T>(
        IValidator<T>[] validators,
        T model,
        CancellationToken cancellationToken)
    {
        List<ValidationFailure>? failures = null;
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(model, cancellationToken).ConfigureAwait(false);
            if (result.Failures.Count == 0)
            {
                continue;
            }

            failures ??= [];
            failures.AddRange(result.Failures);
        }

        return failures is null ? ValidationResult.Valid : new ValidationResult(failures);
    }

    private delegate object ResolveValidatorsDelegate(IServiceProvider services);

    private delegate ValueTask<ValidationResult> ValidateModelDelegate(
        object validators,
        object model,
        CancellationToken cancellationToken);

    private readonly record struct ValidationCandidate(int ArgumentIndex, int TypePlanIndex);

    private sealed record ValidationTypePlan(
        ResolveValidatorsDelegate ResolveValidators,
        ValidateModelDelegate ValidateAsync);

    private sealed record ValidationPlan(
        ValidationCandidate[] Candidates,
        ValidationTypePlan[] TypePlans);

    private sealed class ValiantValidationEndpointFilter(ValidationPlan plan, EndpointFilterDelegate next)
    {
        public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocationContext)
        {
            return ValidateCandidatesAsync(invocationContext, next, plan);
        }
    }
}
