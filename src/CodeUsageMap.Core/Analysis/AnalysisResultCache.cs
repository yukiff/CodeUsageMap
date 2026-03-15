using System.Collections.Concurrent;
using CodeUsageMap.Contracts.Analysis;

namespace CodeUsageMap.Core.Analysis;

internal sealed class AnalysisResultCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);

    public bool TryGet(AnalyzeRequest request, out AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(request);

        result = default!;

        var solutionTimestamp = GetSolutionTimestamp(request.SolutionPath);
        if (!_entries.TryGetValue(CreateKey(request, solutionTimestamp), out var entry))
        {
            return false;
        }

        result = entry.Result;
        return true;
    }

    public void Store(AnalyzeRequest request, AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var solutionTimestamp = GetSolutionTimestamp(request.SolutionPath);
        _entries[CreateKey(request, solutionTimestamp)] = new CacheEntry
        {
            Result = result,
        };
    }

    private static string CreateKey(AnalyzeRequest request, DateTimeOffset solutionTimestamp)
    {
        return string.Join(
            "|",
            request.SolutionPath,
            solutionTimestamp.ToUnixTimeMilliseconds(),
            request.SymbolName,
            request.Options.SymbolIndex?.ToString() ?? string.Empty,
            request.Options.Depth,
            request.Options.ExcludeTests,
            request.Options.ExcludeGenerated,
            request.Options.WorkspaceLoader ?? string.Empty);
    }

    private static DateTimeOffset GetSolutionTimestamp(string solutionPath)
    {
        return File.Exists(solutionPath)
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(solutionPath), TimeSpan.Zero)
            : DateTimeOffset.MinValue;
    }

    private sealed class CacheEntry
    {
        public required AnalysisResult Result { get; init; }
    }
}
