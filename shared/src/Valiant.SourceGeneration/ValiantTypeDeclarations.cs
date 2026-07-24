using System.Text;
using Microsoft.CodeAnalysis;

namespace Valiant.SourceGeneration;

internal static class ValiantTypeDeclarations
{
    public static string GetTypeModifier(INamedTypeSymbol symbol)
    {
        var builder = new StringBuilder();

        switch (symbol.DeclaredAccessibility)
        {
            case Accessibility.Public:
                builder.Append("public ");
                break;
            case Accessibility.Internal:
                builder.Append("internal ");
                break;
            case Accessibility.Private:
                builder.Append("private ");
                break;
            case Accessibility.Protected:
                builder.Append("protected ");
                break;
            case Accessibility.ProtectedAndInternal:
                builder.Append("private protected ");
                break;
            case Accessibility.ProtectedOrInternal:
                builder.Append("protected internal ");
                break;
        }

        if (symbol.IsStatic)
        {
            builder.Append("static ");
            return builder.ToString();
        }

        if (symbol.IsAbstract)
        {
            builder.Append("abstract ");
        }

        if (symbol.IsSealed)
        {
            builder.Append("sealed ");
        }

        return builder.ToString();
    }

    public static string GetTypeDeclarationKeyword(INamedTypeSymbol symbol)
    {
        if (symbol.IsRecord && symbol.TypeKind == TypeKind.Struct)
        {
            return "record struct";
        }

        return symbol.TypeKind switch
        {
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            _ => symbol.IsRecord ? "record" : "class"
        };
    }

    public static string GetTypeDeclarationName(INamedTypeSymbol symbol)
    {
        return symbol.TypeParameters.Length == 0
            ? symbol.Name
            : symbol.Name + "<" + string.Join(", ", symbol.TypeParameters.Select(static parameter => parameter.Name)) + ">";
    }
}
