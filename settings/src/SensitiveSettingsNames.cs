namespace StaticNorth.Valiant.Settings;

internal static class SensitiveSettingsNames
{
    private static readonly string[] SensitiveNameParts =
    [
        "password",
        "secret",
        "token",
        "apikey",
        "api_key",
        "connectionstring",
        "connection_string",
        "clientsecret",
        "client_secret"
    ];

    internal static bool IsSensitive(string propertyName, string configurationKey)
    {
        var name = propertyName.Replace("-", string.Empty)
            .Replace(".", string.Empty);
        var key = configurationKey.Replace("-", string.Empty)
            .Replace(".", string.Empty);

        foreach (var part in SensitiveNameParts)
        {
#if NET8_0_OR_GREATER
            if (name.Contains(part, StringComparison.OrdinalIgnoreCase) ||
                key.Contains(part, StringComparison.OrdinalIgnoreCase))
#else
            if (name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
#endif
            {
                return true;
            }
        }

        return false;
    }
}
