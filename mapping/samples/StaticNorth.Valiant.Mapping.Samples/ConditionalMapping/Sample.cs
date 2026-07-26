namespace StaticNorth.Valiant.Mapping.Samples.ConditionalMapping;

/// <summary>
/// Demonstrates native if/else mapping, a chained <c>When</c> condition, and
/// the equivalent <c>Fallback</c> and <c>Default</c> APIs.
/// </summary>
public static class Sample
{
    public static void Run()
    {
        var mapper = new ContactMapper();
        var hidden = mapper.Map(new Contact
        {
            Name = "Ada",
            Email = "ada@example.test",
            Phone = "+371 555 0100"
        });
        var visible = mapper.Map(new Contact
        {
            UseEmailAsDisplayName = true,
            IncludeContactDetails = true,
            Name = "Ada",
            Email = "ada@example.test",
            Phone = "+371 555 0100"
        });

        Console.WriteLine($"Hidden: {hidden.DisplayName}; {hidden.Email}; {hidden.Phone}");
        Console.WriteLine($"Visible: {visible.DisplayName}; {visible.Email}; {visible.Phone}");
    }
}
