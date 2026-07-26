using Microsoft.CodeAnalysis.CSharp;

namespace StaticNorth.Valiant.SourceGeneration;

internal static class ValiantSourceLiterals
{
    public static string String(string value)
    {
        return SymbolDisplay.FormatLiteral(value, quote: true);
    }
}
