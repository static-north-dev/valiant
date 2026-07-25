namespace Valiant.Mapping.Samples.FlatteningAndEnums;

/// <summary>
/// Demonstrates convention-based flattening, unflattening, and compatible enum
/// conversion.
/// </summary>
public static class Sample
{
    public static void Run()
    {
        var flattened = new OfficeMapper().Map(
            new Office(new OfficeLocation { City = "Riga" }, OfficeStatus.Open));
        var unflattened = new OfficeImportMapper().Map(
            new OfficeImport { LocationCity = "Tallinn" });

        Console.WriteLine($"Flattened office: {flattened.LocationCity}, {flattened.Status}");
        Console.WriteLine($"Unflattened office: {unflattened.Location?.City}");
    }
}
