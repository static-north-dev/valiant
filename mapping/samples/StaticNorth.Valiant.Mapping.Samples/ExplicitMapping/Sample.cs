namespace Valiant.Mapping.Samples.ExplicitMapping;

/// <summary>
/// Demonstrates explicit member ownership with a custom source transform, a
/// renamed target member, and an intentionally ignored target member.
/// </summary>
public static class Sample
{
    public static void Run()
    {
        var target = new UserProfileMapper().Map(
            new UserProfile("Ada", "Lovelace", "ada@example.test", "private"));

        Console.WriteLine($"Explicit transform: {target.DisplayName}");
        Console.WriteLine($"Renamed member: {target.Email}");
        Console.WriteLine($"Ignored member: {target.InternalNote}");
    }
}
