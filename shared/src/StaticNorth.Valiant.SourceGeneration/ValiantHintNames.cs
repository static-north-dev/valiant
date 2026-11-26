using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;

namespace StaticNorth.Valiant.SourceGeneration;

internal enum ValiantHintNameStyle
{
    NamespaceSeparatorsOnly,
    AllNonAlphanumericCharacters
}

internal static class ValiantHintNames
{
    public static string Create(
        INamedTypeSymbol symbol,
        string suffix,
        ValiantHintNameStyle style = ValiantHintNameStyle.NamespaceSeparatorsOnly)
    {
        var displayName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var prefix = GetIdentityPrefix(displayName, ".");
        if (style == ValiantHintNameStyle.NamespaceSeparatorsOnly)
        {
            return prefix + displayName
                .Replace("global::", string.Empty)
                .Replace('.', '_')
                .Replace('+', '_')
                .Replace('@', '_') + "." + suffix + ".g.cs";
        }

        var builder = new StringBuilder(prefix, prefix.Length + displayName.Length + suffix.Length + ".g.cs".Length);
        foreach (var character in displayName)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.Append('.').Append(suffix).Append(".g.cs").ToString();
    }

    public static string GetIdentityPrefix(string displayName, string separator)
    {
        // Keep hint names for unambiguous inputs and distinguish literal underscores from normalized separators.
        if (!displayName.Any(static character => !char.IsLetterOrDigit(character) && character is not ('.' or ':')))
        {
            return string.Empty;
        }

        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(displayName))).Replace("-", string.Empty) + separator;
    }
}
