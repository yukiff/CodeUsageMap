using CodeUsageMap.Contracts.Diagnostics;

namespace CodeUsageMap.Vsix.ViewModels;

internal sealed class UsageMapDiagnosticItemViewModel
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public AnalysisConfidence Confidence { get; init; } = AnalysisConfidence.Unknown;
}
