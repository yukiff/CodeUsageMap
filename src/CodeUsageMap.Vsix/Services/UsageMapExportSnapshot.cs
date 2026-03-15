using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Presentation;

namespace CodeUsageMap.Vsix.Services
{

internal sealed class UsageMapExportSnapshot
{
    public required AnalyzeRequest Request { get; init; }

    public required AnalysisResult Result { get; init; }

    public required UsageMapViewModel ViewModel { get; init; }
}
}
