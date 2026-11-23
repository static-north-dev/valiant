using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace StaticNorth.Valiant.Validation.AspNetCore;

internal static class ValiantValidatorExecutor
{
    private static readonly MethodInfo ValidateCoreMethod = typeof(ValiantValidatorExecutor)
        .GetMethod(nameof(ValidateCore), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly ConcurrentDictionary<Type, Type> ValidatorEnumerableTypes = new();
    private static readonly ConcurrentDictionary<Type, Func<object, object, ValidationResult>> ValidateDelegates = new();

    public static IEnumerable<object> GetValidators(IServiceProvider services, Type modelType)
    {
        _ = services ?? throw new ArgumentNullException(nameof(services));
        _ = modelType ?? throw new ArgumentNullException(nameof(modelType));

        var enumerableType = ValidatorEnumerableTypes.GetOrAdd(modelType, static type =>
            typeof(IEnumerable<>).MakeGenericType(typeof(IValidator<>).MakeGenericType(type)));
        return ((IEnumerable?)services.GetService(enumerableType))?.Cast<object>() ?? [];
    }

    public static ValidationResult Validate(Type modelType, object validator, object model)
    {
        _ = modelType ?? throw new ArgumentNullException(nameof(modelType));
        _ = validator ?? throw new ArgumentNullException(nameof(validator));
        _ = model ?? throw new ArgumentNullException(nameof(model));

        return ValidateDelegates.GetOrAdd(modelType, CreateValidateDelegate)(validator, model);
    }

    private static Func<object, object, ValidationResult> CreateValidateDelegate(Type modelType)
    {
        return (Func<object, object, ValidationResult>)ValidateCoreMethod
            .MakeGenericMethod(modelType)
            .CreateDelegate(typeof(Func<object, object, ValidationResult>));
    }

    private static ValidationResult ValidateCore<T>(object validator, object model)
    {
        return ((IValidator<T>)validator).Validate((T)model);
    }
}
