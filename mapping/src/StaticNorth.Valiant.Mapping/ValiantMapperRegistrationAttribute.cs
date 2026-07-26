namespace StaticNorth.Valiant.Mapping;

/// <summary>
/// Describes a source-generated mapper pair available in an assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ValiantMapperRegistrationAttribute : Attribute
{
    /// <summary>
    /// Initializes a new mapper registration attribute.
    /// </summary>
    /// <param name="sourceType">The mapper source type.</param>
    /// <param name="targetType">The mapper target type.</param>
    public ValiantMapperRegistrationAttribute(Type sourceType, Type targetType)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
    }

    /// <summary>
    /// Gets the mapper source type.
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// Gets the mapper target type.
    /// </summary>
    public Type TargetType { get; }
}
