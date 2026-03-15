namespace CodeUsageMap.Contracts.Diagnostics;

public sealed class AnalysisDiagnostic
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public AnalysisConfidence Confidence { get; init; } = AnalysisConfidence.Unknown;
}
