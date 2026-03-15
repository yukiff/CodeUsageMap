using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Vsix.ViewModels;

internal sealed class UsageMapRootSearchResultItemViewModel
{
    public required string SolutionPath { get; init; }

    public required string SymbolName { get; init; }

    public required string DisplayName { get; init; }

    public string SymbolKey { get; init; } = string.Empty;

    public NodeKind Kind { get; init; } = NodeKind.Unknown;

    public string ProjectName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public int? LineNumber { get; init; }
}
