namespace StaticNorth.Valiant.Mapping.Samples.NullHandling;

/// <summary>
/// Demonstrates mapper options for null source values and the per-rule
/// <c>IgnoreNull</c>, <c>MapNull</c>, and <c>AllowNull</c> overrides, including
/// a fallback for an ignored null.
/// </summary>
public static class Sample
{
    public static void Run()
    {
        var source = new Profile();
        var mapNulls = new ProfileMapper(new ValiantMappingOptions
        {
            NullHandling = ValiantNullHandling.MapNulls
        }).Map(source);
        var ignoreNulls = new ProfileMapper(new ValiantMappingOptions
        {
            NullHandling = ValiantNullHandling.IgnoreSourceNulls
        }).Map(source);

        Console.WriteLine($"Map nulls default: {mapNulls.DefaultValue ?? "<null>"}");
        Console.WriteLine($"Ignore nulls default: {ignoreNulls.DefaultValue}");
        Console.WriteLine($"Per-rule ignore: {mapNulls.IgnoredValue}");
        Console.WriteLine($"Per-rule map/allow: {ignoreNulls.MappedValue ?? "<null>"}, {ignoreNulls.AllowedValue ?? "<null>"}");
        Console.WriteLine($"Ignored null fallback: {ignoreNulls.FallbackValue}");
    }
}
