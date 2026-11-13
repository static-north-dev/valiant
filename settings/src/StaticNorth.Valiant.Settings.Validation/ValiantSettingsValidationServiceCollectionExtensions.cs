using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StaticNorth.Valiant.Validation;

namespace StaticNorth.Valiant.Settings.Validation;

/// <summary>
/// Registers Valiant validators as ASP.NET Core options validators for settings models.
/// </summary>
public static class ValiantSettingsValidationServiceCollectionExtensions
{
    /// <summary>
    /// Adds a Valiant-backed options validator for a settings model when validation integration is enabled.
    /// </summary>
    public static IServiceCollection AddValiantSettingsOptionsValidation<TOptions>(
        this IServiceCollection services,
        ValiantSettingsValidationOptions options)
        where TOptions : class
    {
        _ = services ?? throw new ArgumentNullException(nameof(services));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        if (!options.ValidateWithValiant)
        {
            return services;
        }

        services.AddSingleton<IValidateOptions<TOptions>>(serviceProvider =>
            new ValiantValidateOptions<TOptions>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                options.RequireValidators));

        return services;
    }

    private sealed class ValiantValidateOptions<TOptions>(
        IServiceScopeFactory scopeFactory,
        bool requireValidator) :
#if NET11_0_OR_GREATER
        IAsyncValidateOptions<TOptions>
#else
        IValidateOptions<TOptions>
#endif
        where TOptions : class
    {
        public ValidateOptionsResult Validate(string? name, TOptions options)
        {
            if (options is null)
            {
                return ValidateOptionsResult.Fail($"Options instance for {typeof(TOptions).FullName} was null.");
            }

            using var scope = scopeFactory.CreateScope();
            var validator = scope.ServiceProvider.GetService<IValidator<TOptions>>();
            return validator is null
                ? MissingValidatorResult()
                : ToOptionsResult(validator.Validate(options));
        }

#if NET11_0_OR_GREATER
        public async Task<ValidateOptionsResult> ValidateAsync(
            string? name,
            TOptions options,
            CancellationToken cancellationToken = default)
        {
            if (options is null)
            {
                return ValidateOptionsResult.Fail($"Options instance for {typeof(TOptions).FullName} was null.");
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var validator = scope.ServiceProvider.GetService<IValidator<TOptions>>();
            return validator is null
                ? MissingValidatorResult()
                : ToOptionsResult(await validator.ValidateAsync(options, cancellationToken).ConfigureAwait(false));
        }
#endif

        private ValidateOptionsResult MissingValidatorResult()
        {
            return requireValidator
                ? ValidateOptionsResult.Fail($"No Valiant validator was registered for {typeof(TOptions).FullName}.")
                : ValidateOptionsResult.Success;
        }

        private static ValidateOptionsResult ToOptionsResult(ValidationResult result)
        {
            return result.IsValid
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(result.Errors.Select(FormatFailure));
        }

        private static string FormatFailure(ValidationFailure failure)
        {
            return string.IsNullOrWhiteSpace(failure.PropertyName)
                ? failure.ErrorMessage
                : failure.PropertyName + ": " + failure.ErrorMessage;
        }
    }
}
