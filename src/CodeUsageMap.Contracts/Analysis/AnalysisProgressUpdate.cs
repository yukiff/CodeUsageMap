namespace CodeUsageMap.Contracts.Analysis;

public sealed class AnalysisProgressUpdate
{
    public AnalysisProgressStage Stage { get; init; }

    public string Message { get; init; } = string.Empty;

    public string SymbolName { get; init; } = string.Empty;

    public int? Depth { get; init; }

    public int? ExpandedSymbols { get; init; }
}
