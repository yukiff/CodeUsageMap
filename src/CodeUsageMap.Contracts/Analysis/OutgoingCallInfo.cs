using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Analysis
{

public sealed class OutgoingCallInfo
{
    public string DisplayName { get; init; } = string.Empty;

    public string SymbolKey { get; init; } = string.Empty;

    public NodeKind TargetKind { get; init; } = NodeKind.Unknown;

    public string ProjectName { get; init; } = string.Empty;

    public string NamespaceName { get; init; } = string.Empty;

    public string Accessibility { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public int? LineNumber { get; init; }

    public EdgeKind Kind { get; init; } = EdgeKind.DirectCall;

    public string ReferenceText { get; init; } = string.Empty;

    public string SymbolOrigin { get; init; } = "source";

    public bool NormalizedFromMetadata { get; init; }

    public string NormalizationStrategy { get; init; } = string.Empty;

    public string AssemblyIdentity { get; init; } = string.Empty;

    public string Limitation { get; init; } = string.Empty;

    public bool ExcludedFromGraph { get; init; }
}
}
