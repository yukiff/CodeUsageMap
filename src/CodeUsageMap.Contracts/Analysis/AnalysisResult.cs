using CodeUsageMap.Contracts.Diagnostics;
using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Analysis;

public sealed class AnalysisResult
{
    public required UsageGraph Graph { get; init; }

    public SymbolResolutionInfo SymbolResolution { get; init; } = new();

    public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; init; } = [];
}
