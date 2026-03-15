using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Diagnostics;

namespace CodeUsageMap.Contracts.Serialization;

public sealed class AnalysisOutputMetadata
{
    public string SchemaVersion { get; init; } = "1.0";

    public required AnalysisOptionsSnapshot AnalysisOptions { get; init; }

    public string WorkspaceLoader { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; init; }

    public bool PartialResult { get; init; }

    public SymbolResolutionInfo SymbolResolution { get; init; } = new();

    public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; init; } = [];
}
