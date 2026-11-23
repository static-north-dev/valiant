using System.Reflection;
using System.Runtime.CompilerServices;

namespace StaticNorth.Valiant.Endpoints.AspNetCore;

internal static class QueryObjectProperties
{
    private static readonly ConditionalWeakTable<Type, PropertyInfo[]> Properties = new();

    internal static PropertyInfo[] Get(Type type) =>
        Properties.GetValue(type, static type => Discover(type));

    private static PropertyInfo[] Discover(Type type)
    {
        var properties = new List<PropertyInfo>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (property.GetIndexParameters().Length == 0 && names.Add(property.Name))
                {
                    properties.Add(property);
                }
            }
        }

        return properties.ToArray();
    }
}
