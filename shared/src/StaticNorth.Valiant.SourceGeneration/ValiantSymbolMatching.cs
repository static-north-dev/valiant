using Microsoft.CodeAnalysis;

namespace StaticNorth.Valiant.SourceGeneration;

internal static class ValiantSymbolMatching
{
    public static bool HasMetadataName(INamedTypeSymbol symbol, string namespaceName, string metadataName)
    {
        return symbol.ContainingNamespace.ToDisplayString() == namespaceName &&
            symbol.MetadataName == metadataName;
    }

    public static bool ImplementsInterface(INamedTypeSymbol symbol, string namespaceName, string metadataName)
    {
        foreach (var interfaceType in symbol.AllInterfaces)
        {
            if (HasMetadataName(interfaceType, namespaceName, metadataName))
            {
                return true;
            }
        }

        return false;
    }
}
