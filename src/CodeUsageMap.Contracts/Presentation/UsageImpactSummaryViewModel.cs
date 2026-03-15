namespace CodeUsageMap.Contracts.Presentation
{

public sealed class UsageImpactSummaryViewModel
{
    public int ReferencingProjectCount { get; init; }

    public int ImplementationCount { get; init; }

    public int OverrideCount { get; init; }

    public bool HasTestReference { get; init; }

    public int IncomingReferenceCount { get; init; }

    public int ComplexityScore { get; init; }
}
}
