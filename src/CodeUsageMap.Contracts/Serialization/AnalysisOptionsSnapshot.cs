namespace CodeUsageMap.Contracts.Serialization
{

public sealed class AnalysisOptionsSnapshot
{
    public required string SolutionPath { get; init; }

    public required string SymbolName { get; init; }

    public int? SymbolIndex { get; init; }

    public int Depth { get; init; }

    public bool ExcludeTests { get; init; }

    public bool ExcludeGenerated { get; init; }
}
}
