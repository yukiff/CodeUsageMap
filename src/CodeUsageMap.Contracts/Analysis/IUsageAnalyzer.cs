namespace CodeUsageMap.Contracts.Analysis
{

public interface IUsageAnalyzer
{
    Task<AnalysisResult> AnalyzeAsync(AnalyzeRequest request, CancellationToken cancellationToken);
}
}
