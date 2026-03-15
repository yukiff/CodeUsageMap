using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Contracts.Presentation;

namespace CodeUsageMap.Contracts.Serialization
{

public interface IUsageGraphSerializer
{
    string ToJson(UsageGraph graph);

    string ToDgml(UsageGraph graph);

    string ToJsonDocument(AnalysisResult result, AnalyzeRequest request);

    string ToViewModelJsonDocument(UsageMapViewModel viewModel, AnalysisResult result, AnalyzeRequest request);

    string ToDgmlDocument(AnalysisResult result, AnalyzeRequest request);
}
}
