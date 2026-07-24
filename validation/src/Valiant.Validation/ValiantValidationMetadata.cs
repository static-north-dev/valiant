namespace Valiant.Validation;

/// <summary>
/// Provides source-generated validation metadata for OpenAPI and other tooling integrations.
/// </summary>
public interface IValiantValidationMetadataProvider
{
    /// <summary>
    /// Gets the model type described by this metadata provider.
    /// </summary>
    Type ModelType { get; }

    /// <summary>
    /// Gets the property validation metadata for the model type.
    /// </summary>
    IReadOnlyList<ValiantPropertyValidationMetadata> Properties { get; }
}

/// <summary>
/// Describes the validation checks declared for one model property.
/// </summary>
public sealed class ValiantPropertyValidationMetadata
{
    /// <summary>
    /// Initializes property validation metadata.
    /// </summary>
    public ValiantPropertyValidationMetadata(
        string propertyName,
        Type propertyType,
        bool isConditional,
        IReadOnlyList<ValiantValidationCheckMetadata> checks)
    {
        PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        PropertyType = propertyType ?? throw new ArgumentNullException(nameof(propertyType));
        IsConditional = isConditional;
        Checks = checks ?? throw new ArgumentNullException(nameof(checks));
    }

    /// <summary>
    /// Gets the CLR property name or property path.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the CLR property type.
    /// </summary>
    public Type PropertyType { get; }

    /// <summary>
    /// Gets whether the property was declared under a condition, switch, or loop.
    /// </summary>
    public bool IsConditional { get; }

    /// <summary>
    /// Gets the checks declared for the property.
    /// </summary>
    public IReadOnlyList<ValiantValidationCheckMetadata> Checks { get; }
}

/// <summary>
/// Describes one validation check declared in a property chain.
/// </summary>
public sealed class ValiantValidationCheckMetadata
{
    /// <summary>
    /// Initializes validation check metadata.
    /// </summary>
    public ValiantValidationCheckMetadata(
        string kind,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> typeArguments,
        string? code,
        string? message,
        string? severity,
        bool isAsync,
        bool isConditional)
    {
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        TypeArguments = typeArguments ?? throw new ArgumentNullException(nameof(typeArguments));
        Code = code;
        Message = message;
        Severity = severity;
        IsAsync = isAsync;
        IsConditional = isConditional;
    }

    /// <summary>
    /// Gets the validation check name.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets source-level validation arguments for statically describable checks.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets source-level type arguments for generic checks.
    /// </summary>
    public IReadOnlyList<string> TypeArguments { get; }

    /// <summary>
    /// Gets the literal error code when it can be represented statically.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// Gets the literal error message when it can be represented statically.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets the severity expression or literal declared for this check.
    /// </summary>
    public string? Severity { get; }

    /// <summary>
    /// Gets whether this check runs asynchronously.
    /// </summary>
    public bool IsAsync { get; }

    /// <summary>
    /// Gets whether this check was declared under a condition, switch, or loop.
    /// </summary>
    public bool IsConditional { get; }
}
