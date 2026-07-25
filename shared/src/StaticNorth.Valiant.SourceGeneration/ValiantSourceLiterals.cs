using Microsoft.CodeAnalysis.CSharp;

namespace Valiant.SourceGeneration;

internal static class ValiantSourceLiterals
{
    public static string String(string value)
    {
        return SymbolDisplay.FormatLiteral(value, quote: true);
    }
}
