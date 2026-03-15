namespace CodeUsageMap.Contracts.Presentation;

public sealed class UsageNodeAssessmentViewModel
{
    public UsageImpactSummaryViewModel Impact { get; init; } = new();

    public UsageRiskSummaryViewModel Risk { get; init; } = new();
}
