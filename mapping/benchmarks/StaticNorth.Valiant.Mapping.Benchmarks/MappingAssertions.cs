namespace StaticNorth.Valiant.Mapping.Benchmarks;

internal static class MappingAssertions
{
    public static void Equal(FlatTarget expected, FlatTarget actual, string implementation)
    {
        if (expected.Id != actual.Id
            || expected.Name != actual.Name
            || expected.Email != actual.Email
            || expected.Age != actual.Age
            || expected.IsActive != actual.IsActive
            || expected.Balance != actual.Balance
            || expected.CreatedAt != actual.CreatedAt
            || expected.Score != actual.Score)
        {
            throw new InvalidOperationException(
                $"{implementation} did not map every flat member correctly.");
        }
    }

    public static void Equal(OrderTarget expected, OrderTarget actual, string implementation)
    {
        if (expected.Id != actual.Id
            || expected.CustomerName != actual.CustomerName
            || !Equal(expected.PrimaryItem, actual.PrimaryItem)
            || expected.Items.Count != actual.Items.Count)
        {
            throw new InvalidOperationException(
                $"{implementation} did not map the nested object graph correctly.");
        }

        for (var index = 0; index < expected.Items.Count; index++)
        {
            if (!Equal(expected.Items[index], actual.Items[index]))
            {
                throw new InvalidOperationException(
                    $"{implementation} did not map collection element {index} correctly.");
            }
        }
    }

    public static void Equal(
        IEnumerable<LineItemTarget> expected,
        IEnumerable<LineItemTarget> actual,
        string implementation,
        bool preserveOrder)
    {
        var expectedItems = preserveOrder
            ? expected.ToArray()
            : expected.OrderBy(static item => item.Sku, StringComparer.Ordinal).ToArray();
        var actualItems = preserveOrder
            ? actual.ToArray()
            : actual.OrderBy(static item => item.Sku, StringComparer.Ordinal).ToArray();
        if (expectedItems.Length != actualItems.Length)
        {
            throw new InvalidOperationException(
                $"{implementation} mapped a different collection length.");
        }

        for (var index = 0; index < expectedItems.Length; index++)
        {
            if (!Equal(expectedItems[index], actualItems[index]))
            {
                throw new InvalidOperationException(
                    $"{implementation} did not map collection element {index} correctly.");
            }
        }
    }

    private static bool Equal(LineItemTarget expected, LineItemTarget actual)
    {
        return expected.Sku == actual.Sku
            && expected.Quantity == actual.Quantity
            && expected.Price == actual.Price;
    }
}
