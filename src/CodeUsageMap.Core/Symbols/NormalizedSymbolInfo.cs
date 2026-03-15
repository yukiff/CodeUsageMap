using Microsoft.CodeAnalysis;

namespace CodeUsageMap.Core.Symbols;

internal sealed class NormalizedSymbolInfo
{
    public required ISymbol Symbol { get; init; }

    public string SymbolOrigin { get; init; } = "source";

    public bool NormalizedFromMetadata { get; init; }

    public string NormalizationStrategy { get; init; } = string.Empty;

    public string AssemblyIdentity { get; init; } = string.Empty;

    public string Limitation { get; init; } = string.Empty;
}
