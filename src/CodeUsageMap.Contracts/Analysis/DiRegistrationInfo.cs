using CodeUsageMap.Contracts.Diagnostics;
using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Analysis;

public sealed class DiRegistrationInfo
{
    public required string RegistrationId { get; init; }

    public required string RegistrationDisplayName { get; init; }

    public required string ServiceSymbolId { get; init; }

    public required string ServiceDisplayName { get; init; }

    public NodeKind ServiceKind { get; init; } = NodeKind.Interface;

    public required string ImplementationSymbolId { get; init; }

    public required string ImplementationDisplayName { get; init; }

    public NodeKind ImplementationKind { get; init; } = NodeKind.Class;

    public string Lifetime { get; init; } = string.Empty;

    public string RegistrationKind { get; init; } = string.Empty;

    public string ProjectName { get; init; } = string.Empty;

    public string NamespaceName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public int? LineNumber { get; init; }

    public string RegistrationText { get; init; } = string.Empty;

    public AnalysisConfidence Confidence { get; init; } = AnalysisConfidence.High;
}
