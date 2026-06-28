using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Valiant.AspNetCore;

/// <summary>
/// MVC validation extensions for Valiant validators.
/// </summary>
public static class ValiantMvcExtensions
{
    /// <summary>
    /// Adds Valiant model validation to MVC.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddValiantMvcValidation(this IServiceCollection services)
    {
        _ = services ?? throw new ArgumentNullException(nameof(services));

        services.Configure<MvcOptions>(static options =>
        {
            if (!options.ModelValidatorProviders.OfType<ValiantModelValidatorProvider>().Any())
            {
                options.ModelValidatorProviders.Add(new ValiantModelValidatorProvider());
            }
        });

        return services;
    }

    /// <summary>
    /// Adds Valiant model validation to MVC.
    /// </summary>
    /// <param name="builder">The MVC builder.</param>
    /// <returns>The MVC builder.</returns>
    public static IMvcBuilder AddValiantValidation(this IMvcBuilder builder)
    {
        _ = builder ?? throw new ArgumentNullException(nameof(builder));
        builder.Services.AddValiantMvcValidation();
        return builder;
    }

    /// <summary>
    /// Adds Valiant model validation to MVC Core.
    /// </summary>
    /// <param name="builder">The MVC Core builder.</param>
    /// <returns>The MVC Core builder.</returns>
    public static IMvcCoreBuilder AddValiantValidation(this IMvcCoreBuilder builder)
    {
        _ = builder ?? throw new ArgumentNullException(nameof(builder));
        builder.Services.AddValiantMvcValidation();
        return builder;
    }
}

internal sealed class ValiantModelValidatorProvider : IModelValidatorProvider
{
    public void CreateValidators(ModelValidatorProviderContext context)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));

        var modelType = context.ModelMetadata.ModelType;
        if (modelType == typeof(string) || modelType.IsValueType)
        {
            return;
        }

        context.Results.Add(new ValidatorItem
        {
            IsReusable = true,
            Validator = new ValiantModelValidator(modelType)
        });
    }
}

internal sealed class ValiantModelValidator(Type modelType) : IModelValidator
{
    public IEnumerable<ModelValidationResult> Validate(ModelValidationContext context)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));
        if (context.Model is null)
        {
            return [];
        }

        var results = new List<ModelValidationResult>();
        foreach (var validator in ValiantValidatorExecutor.GetValidators(context.ActionContext.HttpContext.RequestServices, modelType))
        {
            var result = ValiantValidatorExecutor.Validate(modelType, validator, context.Model);
            foreach (var failure in result.Errors)
            {
                results.Add(new ModelValidationResult(failure.PropertyName, failure.ErrorMessage));
            }
        }

        return results;
    }
}
