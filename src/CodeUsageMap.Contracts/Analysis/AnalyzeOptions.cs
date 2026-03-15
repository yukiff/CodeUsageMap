namespace CodeUsageMap.Contracts.Analysis
{

public sealed class AnalyzeOptions
{
    public int Depth { get; init; } = 1;

    public int? SymbolIndex { get; init; }

    public bool ExcludeTests { get; init; }

    public bool ExcludeGenerated { get; init; }

    public string WorkspaceLoader { get; init; } = string.Empty;
}
}
