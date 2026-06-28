using System.Collections;
using System.Reflection;

namespace Valiant.AspNetCore;

internal static class ValiantValidatorExecutor
{
    private static readonly MethodInfo ValidateCoreMethod = typeof(ValiantValidatorExecutor)
        .GetMethod(nameof(ValidateCore), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo ValidateAsyncCoreMethod = typeof(ValiantValidatorExecutor)
        .GetMethod(nameof(ValidateAsyncCore), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static IEnumerable<object> GetValidators(IServiceProvider services, Type modelType)
    {
        _ = services ?? throw new ArgumentNullException(nameof(services));
        _ = modelType ?? throw new ArgumentNullException(nameof(modelType));

        var validatorType = typeof(IValidator<>).MakeGenericType(modelType);
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(validatorType);
        return ((IEnumerable?)services.GetService(enumerableType))?.Cast<object>() ?? [];
    }

    public static ValidationResult Validate(Type modelType, object validator, object model)
    {
        return (ValidationResult)ValidateCoreMethod
            .MakeGenericMethod(modelType)
            .Invoke(null, [validator, model])!;
    }

    public static ValueTask<ValidationResult> ValidateAsync(
        Type modelType,
        object validator,
        object model,
        CancellationToken cancellationToken)
    {
        return (ValueTask<ValidationResult>)ValidateAsyncCoreMethod
            .MakeGenericMethod(modelType)
            .Invoke(null, [validator, model, cancellationToken])!;
    }

    private static ValidationResult ValidateCore<T>(IValidator<T> validator, object model)
    {
        return validator.Validate((T)model);
    }

    private static ValueTask<ValidationResult> ValidateAsyncCore<T>(
        IValidator<T> validator,
        object model,
        CancellationToken cancellationToken)
    {
        return validator.ValidateAsync((T)model, cancellationToken);
    }
}
