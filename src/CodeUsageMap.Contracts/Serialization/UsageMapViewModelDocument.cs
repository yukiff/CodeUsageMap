using CodeUsageMap.Contracts.Presentation;

namespace CodeUsageMap.Contracts.Serialization
{

public sealed class UsageMapViewModelDocument
{
    public required AnalysisOutputMetadata Metadata { get; init; }

    public required UsageMapViewModel ViewModel { get; init; }
}
}
