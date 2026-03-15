namespace CodeUsageMap.Contracts.Presentation
{

public sealed class UsageMapSummaryViewModel
{
    public int NodeCount { get; init; }

    public int EdgeCount { get; init; }

    public int IncomingCount { get; init; }

    public int OutgoingCount { get; init; }

    public int RelatedCount { get; init; }

    public int DiagnosticCount { get; init; }
}
}
