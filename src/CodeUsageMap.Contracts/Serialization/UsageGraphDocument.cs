using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Serialization
{

public sealed class UsageGraphDocument
{
    public required AnalysisOutputMetadata Metadata { get; init; }

    public required UsageGraph Graph { get; init; }
}
}
