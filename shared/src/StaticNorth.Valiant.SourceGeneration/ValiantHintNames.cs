using System.Text;
using Microsoft.CodeAnalysis;

namespace Valiant.SourceGeneration;

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
        if (style == ValiantHintNameStyle.NamespaceSeparatorsOnly)
        {
            return displayName
                .Replace("global::", string.Empty)
                .Replace('.', '_')
                .Replace('+', '_') + "." + suffix + ".g.cs";
        }

        var builder = new StringBuilder(displayName.Length + suffix.Length + ".g.cs".Length);
        foreach (var character in displayName)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.Append('.').Append(suffix).Append(".g.cs").ToString();
    }
}
