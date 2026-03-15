namespace CodeUsageMap.Contracts.Analysis
{

public enum AnalysisProgressStage
{
    LoadingSolution,
    ResolvingSymbol,
    CollectingReferences,
    CollectingImplementations,
    CollectingDiRegistrations,
    CollectingEvents,
    ResolvingExpansion,
    Completed,
}
}
