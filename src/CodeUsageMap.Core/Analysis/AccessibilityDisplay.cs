using Microsoft.CodeAnalysis;

namespace CodeUsageMap.Core.Analysis;

internal static class AccessibilityDisplay
{
    public static string ToDisplayValue(ISymbol? symbol)
    {
        return symbol is null ? string.Empty : ToDisplayValue(symbol.DeclaredAccessibility);
    }

    public static string ToDisplayValue(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "Public",
            Accessibility.Internal => "Internal",
            Accessibility.Private => "Private",
            Accessibility.Protected => "Protected",
            Accessibility.ProtectedAndInternal => "PrivateProtected",
            Accessibility.ProtectedOrInternal => "ProtectedInternal",
            _ => string.Empty,
        };
    }
}
