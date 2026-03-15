namespace CodeUsageMap.Contracts.Analysis;

public sealed class AnalyzeRequest
{
    public required string SolutionPath { get; init; }

    public required string SymbolName { get; init; }

    public AnalyzeOptions Options { get; init; } = new();

    public IProgress<AnalysisProgressUpdate>? Progress { get; init; }
}
