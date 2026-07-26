namespace StaticNorth.Valiant.Mapping.Samples.Composition;

/// <summary>
/// Demonstrates constructor-injected mapper reuse for both a nested object and
/// collection elements.
/// </summary>
public static class Sample
{
    public static void Run()
    {
        var mapper = new CustomerMapper(new AddressMapper());
        var target = mapper.Map(new Customer
        {
            PrimaryAddress = new Address("1 Compiler Way"),
            Addresses =
            [
                new Address("2 Runtime Road"),
                new Address("3 Generator Lane")
            ]
        });

        Console.WriteLine($"Nested mapper: {target.PrimaryAddress?.Line1}");
        Console.WriteLine($"Collection mapper: {string.Join(", ", target.Addresses.Select(address => address.Line1))}");
    }
}
