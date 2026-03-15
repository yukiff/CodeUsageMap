namespace CodeUsageMap.Contracts.Presentation;

public sealed class UsageRiskSummaryViewModel
{
    public int RiskScore { get; init; }

    public string RiskLevel { get; init; } = string.Empty;

    public bool IsPublicApi { get; init; }

    public IReadOnlyList<string> Drivers { get; init; } = [];
}
