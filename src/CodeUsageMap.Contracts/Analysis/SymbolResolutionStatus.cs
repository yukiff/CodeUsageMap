namespace CodeUsageMap.Contracts.Analysis
{

public enum SymbolResolutionStatus
{
    Unspecified = 0,
    Resolved,
    NotFound,
    Ambiguous,
    InvalidSelection,
}
}
