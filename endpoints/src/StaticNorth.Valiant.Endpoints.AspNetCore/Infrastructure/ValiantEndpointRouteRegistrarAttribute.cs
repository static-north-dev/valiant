using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure;

/// <summary>
/// Infrastructure marker used to locate a source-generated endpoint registrar.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ValiantEndpointRouteRegistrarAttribute : Attribute
{
    /// <summary>
    /// Initializes a generated endpoint registrar marker.
    /// </summary>
    /// <param name="registrarType">The generated registrar implementation type.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ValiantEndpointRouteRegistrarAttribute(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type registrarType)
    {
        ArgumentNullException.ThrowIfNull(registrarType);

        if (!typeof(IValiantEndpointRouteRegistrar).IsAssignableFrom(registrarType))
        {
            throw new ArgumentException("The registrar type must implement IValiantEndpointRouteRegistrar.", nameof(registrarType));
        }

        RegistrarType = registrarType;
    }

    /// <summary>
    /// Gets the generated registrar implementation type.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type RegistrarType { get; }
}
