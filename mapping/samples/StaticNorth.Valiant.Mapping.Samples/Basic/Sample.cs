namespace Valiant.Mapping.Samples.Basic;

/// <summary>
/// Demonstrates the smallest useful Valiant mapper: map matching members by
/// convention into an immutable constructor target.
/// </summary>
public static class Sample
{
    public static void Run()
    {
        var target = new PersonMapper().Map(new Person("Ada", 36));

        Console.WriteLine($"Convention mapping: {target.Name}, age {target.Age}");
    }
}
