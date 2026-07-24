namespace Valiant.Validation;

/// <summary>
/// Marks a partial type as a source-generated validator.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ValiantValidatorAttribute : Attribute
{
    /// <summary>
    /// Initializes a new validator marker.
    /// </summary>
    public ValiantValidatorAttribute()
    {
    }

    /// <summary>
    /// Initializes a new validator marker.
    /// </summary>
    /// <param name="modelType">The model type validated by the marked validator.</param>
    public ValiantValidatorAttribute(Type modelType)
    {
        ModelType = modelType ?? throw new ArgumentNullException(nameof(modelType));
    }

    /// <summary>
    /// Gets the model type validated by the marked validator when supplied by the compatibility constructor.
    /// </summary>
    public Type? ModelType { get; }

    /// <summary>
    /// Gets or sets the validation configuration method name. Defaults to <c>Configure</c>.
    /// </summary>
    public string ConfigureMethod { get; set; } = "Configure";

    /// <summary>
    /// Gets or sets whether this validator is registered as the default validator for its model type.
    /// </summary>
    public bool RegisterAsDefault { get; set; } = true;
}

/// <summary>
/// Marks a partial type as a source-generated validator for <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The model type validated by the marked validator.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ValiantValidatorAttribute<T> : ValiantValidatorAttribute
{
    /// <summary>
    /// Initializes a new generic validator marker.
    /// </summary>
    public ValiantValidatorAttribute()
        : base(typeof(T))
    {
    }
}
