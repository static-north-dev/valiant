using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace StaticNorth.Valiant.Mapping.Infrastructure;

/// <summary>
/// Sets an init-only property when the generated mapper cannot use an object initializer.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ValiantInitProperty
{
    /// <summary>Assigns a mapped value to a public init-only property.</summary>
    /// <typeparam name="TTarget">The mapping target type.</typeparam>
    /// <typeparam name="TValue">The property type.</typeparam>
    /// <param name="target">The target being initialized.</param>
    /// <param name="propertyName">The property selected by the generator.</param>
    /// <param name="value">The mapped value.</param>
    public static void Set<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TTarget, TValue>(
        ref TTarget target, string propertyName, TValue value)
    {
        var setter = Setters<TTarget>.ByName.GetOrAdd(propertyName, static name => typeof(TTarget).GetProperty(name)!.SetMethod!);
        object boxedTarget = target!;
        try
        {
            setter.Invoke(boxedTarget, [value]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }

        target = (TTarget)boxedTarget;
    }

    private static class Setters<TTarget>
    {
        internal static readonly ConcurrentDictionary<string, MethodInfo> ByName = new(StringComparer.Ordinal);
    }
}
