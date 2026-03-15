using CodeUsageMap.Contracts.Analysis;
using Microsoft.CodeAnalysis;

namespace CodeUsageMap.Core.Symbols
{

public sealed class SymbolResolutionMatch
{
    public required ResolvedSymbol Symbol { get; init; }

    public required ISymbol RoslynSymbol { get; init; }

    public required SymbolResolutionInfo Resolution { get; init; }
}
}
