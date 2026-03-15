namespace CodeUsageMap.Cli.Options;

public sealed class AnalyzeCommandOptions
{
    public string SolutionPath { get; init; } = string.Empty;

    public string SymbolName { get; init; } = string.Empty;

    public string Format { get; init; } = "json";

    public string OutputPath { get; init; } = string.Empty;

    public int Depth { get; init; } = 1;

    public int? SymbolIndex { get; init; }

    public bool ExcludeTests { get; init; }

    public bool ExcludeGenerated { get; init; }

    public string WorkspaceLoader { get; init; } = string.Empty;
}
